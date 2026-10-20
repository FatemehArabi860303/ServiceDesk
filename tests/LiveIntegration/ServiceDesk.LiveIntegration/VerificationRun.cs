using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Notification.Contracts;
using RabbitMQ.Client;
using RabbitMq.Publishers;
using ServiceDesk.Contracts;
using ServiceDesk.Core.AiAnalysis;
using ServiceDesk.Core.Tickets;
using ServiceDesk.Repository.AiAnalysis;
using ServiceDesk.Repository.Notifications;
using ServiceDesk.Messaging;
using ServiceDesk.Shell.Notifications;

namespace ServiceDesk.LiveIntegration;

internal sealed class VerificationRun(RunEnvironment environment, RunReport report)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }, WriteIndented = true
    };
    private string customerToken = "";
    private string employeeToken = "";
    private IHost? ai;
    private IHost? outbox;
    private Guid ticketId;
    private OutboxMessage baselineEvent = null!;
    private string beforeAnalysis = "";

    public static async Task<int> ExecuteAsync()
    {
        var environment = new RunEnvironment();
        var report = new RunReport(environment.RunId);
        var run = new VerificationRun(environment, report);
        var code = 1;
        try
        {
            await run.ExecuteScenariosAsync();
            report.Overall = report.Scenarios.Any(item => item.Status == "BLOCKED") ? "BLOCKED" : "PASS";
            code = report.Overall == "PASS" ? 0 : 2;
        }
        catch (Exception exception)
        {
            // Exception messages and raw application/provider logs may contain credentials.
            report.Overall = "FAIL";
            report.Diagnostics.Add($"Runner stopped with {exception.GetType().Name}. See scenario statuses and identifiers.");
        }
        finally
        {
            try { await environment.DisposeAsync(); }
            catch (Exception exception)
            {
                report.Overall = "FAIL";
                report.Scenarios.Single(item => item.Name == "Cleanup").Status = "FAIL";
                report.Diagnostics.Add($"Owned host cleanup failed: {exception.GetType().Name}.");
                code = 1;
            }
            await run.SaveAsync();
        }
        return code;
    }

    private async Task ExecuteScenariosAsync()
    {
        await ScenarioAsync("Infrastructure", async () =>
        {
            await environment.PrepareAsync();
            await environment.StartApiAsync();
            var administrator = await LoginAsync($"administrator-{environment.RunId}@example.invalid");
            customerToken = await CreateUserAsync(administrator, environment.CustomerEmail, 0);
            employeeToken = await CreateUserAsync(administrator, $"employee-{environment.RunId}@example.invalid", 1);
        });
        await ScenarioAsync("Ticket creation", async () =>
        {
            // Neither AI worker nor publisher has started. HTTP success cannot depend on AI.
            ticketId = await SubmitAsync("baseline");
            await using var database = environment.Database();
            Require(await database.Tickets.AnyAsync(ticket => ticket.Id == ticketId), "Ticket persistence");
            Require(!await database.TicketAiAnalyses.AnyAsync(), "AI must not run during submission");
            report.Evidence["BaselineTicketId"] = ticketId.ToString();
        });
        await ScenarioAsync("Atomic Outbox", async () =>
        {
            baselineEvent = await EventAsync(ticketId);
            Require(baselineEvent.PublishedAt is null && baselineEvent.ClaimToken is null, "New Outbox must be pending");
            var request = JsonSerializer.Deserialize<TicketAiAnalysisRequestedV1>(baselineEvent.Payload)!;
            Require(request.Id == baselineEvent.Id && request.TicketId == ticketId, "Event identity");
            report.Evidence["BaselineEventId"] = baselineEvent.Id.ToString();
            await using var database = environment.Database();
            // Isolated database only. NOCHECK permits the pre-existing baseline row, while all
            // new AI-request inserts fail until this test-owned constraint is removed.
            await database.Database.ExecuteSqlRawAsync("ALTER TABLE [OutboxMessages] WITH NOCHECK ADD CONSTRAINT [Verification_RejectOutbox] CHECK ([Type] <> 'ticket.ai-analysis.requested.v1')");
            try
            {
                using var response = await ApiAsync("/api/tickets", new { title = Marker("atomic-failure"), description = "Controlled rollback verification", priority = 2 }, customerToken);
                Require((int)response.StatusCode >= 500, "Rejected Outbox insert must fail submission");
                Require(!await database.Tickets.AnyAsync(ticket => ticket.Title == Marker("atomic-failure")), "Ticket rollback");
                Require(await database.OutboxMessages.CountAsync() == 1, "Outbox rollback");
            }
            finally { await database.Database.ExecuteSqlRawAsync("ALTER TABLE [OutboxMessages] DROP CONSTRAINT [Verification_RejectOutbox]"); }
            beforeAnalysis = await SnapshotAsync(ticketId);
        });
        await ScenarioAsync("RabbitMQ publication", async () =>
        {
            outbox = await environment.StartOutboxAsync();
            await WaitPublishedAsync(ticketId);
            Require(environment.Outbox.Confirmed.ContainsKey(baselineEvent.Id), "Outbox marking must follow successful transport confirmation");
            // Inspect while no AI consumer exists, requeue without acknowledging the event.
            using var connection = environment.Factory().CreateConnection();
            using var channel = connection.CreateModel();
            var delivery = channel.BasicGet(RabbitMqOutboxRoute.AiAnalysis.QueueName, autoAck: false);
            Require(delivery is not null, "Published event must reach AI queue");
            Require(delivery!.Exchange == RabbitMqOutboxRoute.AiAnalysis.ExchangeName
                && delivery.RoutingKey == TicketAiAnalysisRequestedV1.Type
                && delivery.BasicProperties.Persistent
                && delivery.BasicProperties.MessageId == baselineEvent.Id.ToString()
                && delivery.BasicProperties.Type == TicketAiAnalysisRequestedV1.Type
                && delivery.BasicProperties.ContentType == "application/json"
                && System.Text.Encoding.UTF8.GetString(delivery.Body.Span) == baselineEvent.Payload, "Published envelope");
            channel.BasicNack(delivery.DeliveryTag, multiple: false, requeue: true);
            Require(await AnalysisAsync(ticketId) is null, "Publisher confirmation is not consumer completion");
        });
        await ScenarioAsync("AI processing", async () =>
        {
            ai = await environment.StartAiAsync();
            await environment.UntilAsync("AI result", async () => await AnalysisAsync(ticketId) is not null);
            Require(environment.Analyzer.Invocations.GetValueOrDefault(ticketId) == 1, "Analyzer invocation");
        });
        await ScenarioAsync("Durable result", async () =>
        {
            var result = await AnalysisAsync(ticketId);
            Require(result is not null && result.EventId == baselineEvent.Id && result.TicketId == ticketId && result.CompletedAt != default, "Durable identity");
            var parsed = JsonSerializer.Deserialize<TicketAiAnalysisResult>(result!.ResultJson, Json)!;
            ValidateTicketAiAnalysisCore.Execute(parsed);
            Require(JsonSerializer.Serialize(parsed, Json) == JsonSerializer.Serialize(ControlledAnalyzer.Result, Json), "Deterministic result");
            Require(await SnapshotAsync(ticketId) == beforeAnalysis, "AI must not modify ticket business state or history");
        });
        await ScenarioAsync("RabbitMQ acknowledgement", () => WaitCompletedAsync(ticketId, 0));
        await ScenarioAsync("Duplicate delivery", async () =>
        {
            var prior = (await AnalysisAsync(ticketId))!;
            var acks = (await QueueAsync()).Acknowledgements;
            await using var publisher = new RabbitMqPublisher(environment.Factory(), new());
            var adapter = new RabbitMqOutboxMessagePublisher(publisher, new([RabbitMqOutboxRoute.AiAnalysis]));
            await adapter.PublishAsync(new OutboxMessageToPublish(baselineEvent.Id, baselineEvent.Type, baselineEvent.Payload));
            await WaitCompletedAsync(ticketId, acks);
            var after = (await AnalysisAsync(ticketId))!;
            Require(after.ResultJson == prior.ResultJson && after.CompletedAt == prior.CompletedAt, "Existing result must not be overwritten");
            Require(environment.Analyzer.Invocations.GetValueOrDefault(ticketId) == 1, "Completed duplicate must bypass analyzer");
        });
        await ScenarioAsync("Notification / Mailpit", async () =>
        {
            using var assigned = await ApiAsync($"/api/tickets/{ticketId}/assignment", null, employeeToken);
            Require(assigned.StatusCode == HttpStatusCode.NoContent, "Real assignment workflow");
            await environment.UntilAsync("Notification Outbox", async () =>
            {
                await using var database = environment.Database();
                return await database.OutboxMessages.AnyAsync(row => row.Type == NotificationRequestedV1.Type && row.PublishedAt != null);
            });
            var prior = await QueueAsync(RabbitMqOutboxRoute.Notification.QueueName);
            await environment.StartNotificationsAsync();
            await environment.UntilAsync("Mailpit delivery", async () =>
            {
                using var messages = await environment.Mailpit.GetFromJsonAsync<JsonDocument>("/api/v1/messages", environment.Deadline.Token);
                foreach (var message in messages!.RootElement.GetProperty("messages").EnumerateArray())
                {
                    if (!HasRecipient(message, environment.CustomerEmail)) continue;
                    var id = message.GetProperty("ID").GetString();
                    using var detail = await environment.Mailpit.GetFromJsonAsync<JsonDocument>($"/api/v1/message/{id}", environment.Deadline.Token);
                    return detail!.RootElement.GetProperty("Subject").GetString() == "Your service request has been assigned"
                        && detail.RootElement.GetProperty("Text").GetString()!.Contains("assigned to a support employee");
                }
                return false;
            });
            await environment.UntilAsync("Notification acknowledgement", async () =>
            {
                var queue = await QueueAsync(RabbitMqOutboxRoute.Notification.QueueName);
                return queue.Acknowledgements > prior.Acknowledgements && queue.Ready == 0 && queue.Unacknowledged == 0;
            });
        });
        await ScenarioAsync("AI failure recovery", async () =>
        {
            await StopAiAsync();
            environment.Analyzer.Reset("Fail");
            var failedTicket = await SubmitAsync("provider-failure");
            await WaitPublishedAsync(failedTicket);
            var acks = (await QueueAsync()).Acknowledgements;
            ai = await environment.StartAiAsync();
            await WaitFailStopAsync(ai, failedTicket, acks);
            // WebApi and publisher remain operational while AI has stopped.
            using var response = await GetApiAsync("/api/tickets", employeeToken);
            Require(response.IsSuccessStatusCode, "Unrelated WebApi availability");
            Require(!outbox!.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping.IsCancellationRequested, "Outbox availability");
            await StopAiAsync();
            environment.Analyzer.Reset("Success");
            ai = await environment.StartAiAsync();
            await WaitCompletedAsync(failedTicket, acks);
        });
        await ScenarioAsync("Persistence failure", async () =>
        {
            await StopAiAsync();
            var failedTicket = await SubmitAsync("persistence-failure");
            await WaitPublishedAsync(failedTicket);
            var acks = (await QueueAsync()).Acknowledgements;
            await using var database = environment.Database();
            // A SQL CHECK failure is at the real analysis insert/commit boundary; it avoids
            // replacing the repository or changing EF's OUTPUT/trigger behavior.
            await database.Database.ExecuteSqlRawAsync($"ALTER TABLE [TicketAiAnalyses] ADD CONSTRAINT [Verification_RejectAnalysis] CHECK ([TicketId] <> '{failedTicket:D}')");
            try
            {
                ai = await environment.StartAiAsync();
                await WaitFailStopAsync(ai, failedTicket, acks);
                await StopAiAsync();
            }
            finally { await database.Database.ExecuteSqlRawAsync("ALTER TABLE [TicketAiAnalyses] DROP CONSTRAINT [Verification_RejectAnalysis]"); }
            ai = await environment.StartAiAsync();
            await WaitCompletedAsync(failedTicket, acks);
        });
        await ScenarioAsync("Broker outage", async () =>
        {
            await StopAiAsync();
            await environment.DockerAsync("stop", "rabbitmq");
            var outageTicket = await SubmitAsync("broker-outage");
            await environment.UntilAsync("Failed publisher claim", async () =>
            {
                var row = await EventAsync(outageTicket);
                Require(row.PublishedAt is null, "No broker confirmation during outage");
                return row.ClaimToken is not null && environment.Outbox.Failed.ContainsKey(row.Id);
            });
            await environment.DockerAsync("start", "rabbitmq");
            foreach (var mapping in new[] { (5672, "SDV_AMQP_PORT"), (15672, "SDV_MANAGEMENT_PORT") })
            {
                Require((await environment.DockerAsync("port", "rabbitmq", mapping.Item1.ToString())).Trim()
                    == "127.0.0.1:" + RunEnvironment.Required(mapping.Item2), "Broker restart must retain owned host ports");
            }
            await environment.UntilAsync("Broker restart readiness", async () =>
            {
                using var response = await environment.Broker.GetAsync("/api/overview", environment.Deadline.Token);
                return response.IsSuccessStatusCode;
            }, 120, retryConnectionErrors: true);
            // Preserve the production five-minute lease. Do not alter Outbox rows to fast-forward it.
            await WaitPublishedAsync(outageTicket, seconds: 420);
            var acks = (await QueueAsync()).Acknowledgements;
            ai = await environment.StartAiAsync();
            await WaitCompletedAsync(outageTicket, acks);
        });
        await ScenarioAsync("Worker shutdown", async () =>
        {
            await StopAiAsync();
            environment.Analyzer.Reset("Hold");
            var cancelledTicket = await SubmitAsync("cancellation");
            await WaitPublishedAsync(cancelledTicket);
            var acks = (await QueueAsync()).Acknowledgements;
            ai = await environment.StartAiAsync();
            await environment.Analyzer.Entered.Task.WaitAsync(TimeSpan.FromSeconds(60));
            await environment.UntilAsync("Active unacknowledged delivery", async () => (await QueueAsync()).Unacknowledged == 1);
            Require(await AnalysisAsync(cancelledTicket) is null, "Pending work must not be completed");
            await StopAiAsync();
            await environment.Analyzer.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await environment.UntilAsync("Cancelled delivery recovery", async () =>
            {
                var state = await QueueAsync();
                return state.Ready == 1 && state.Unacknowledged == 0 && state.Consumers == 0 && state.Acknowledgements == acks;
            });
            environment.Analyzer.Reset("Success");
            ai = await environment.StartAiAsync();
            await WaitCompletedAsync(cancelledTicket, acks);
            var afterRestartAcks = (await QueueAsync()).Acknowledgements;
            await using (var publisher = new RabbitMqPublisher(environment.Factory(), new()))
            {
                var adapter = new RabbitMqOutboxMessagePublisher(publisher, new([RabbitMqOutboxRoute.AiAnalysis]));
                await adapter.PublishAsync(new OutboxMessageToPublish(baselineEvent.Id, baselineEvent.Type, baselineEvent.Payload));
            }
            await WaitCompletedAsync(ticketId, afterRestartAcks);
            Require(environment.Analyzer.Invocations.GetValueOrDefault(ticketId) == 1, "Worker restart must preserve completed-event deduplication");
            await StopAiAsync();
            report.Diagnostics.Add("Graceful cancellation and restart verified; no forced OS crash or concurrent exactly-once execution claim.");
        });
        await RealSmokeAsync();
    }

    private async Task RealSmokeAsync()
    {
        var result = report.Scenarios.Single(item => item.Name == "Real OpenAI smoke test");
        if (!environment.RealOpenAi) { result.Detail = "Optional provider call was not enabled."; return; }
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OpenAI__ApiKey"))
            || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OpenAI__Model")))
        {
            result.Status = "BLOCKED";
            result.Detail = "OpenAI__ApiKey and OpenAI__Model are required; no provider request was made.";
            return;
        }
        // All preceding queues must be drained. Only this one event can reach the real adapter.
        var state = await QueueAsync();
        Require(state.Ready == 0 && state.Unacknowledged == 0 && state.Consumers == 0, "Real smoke requires an empty isolated AI queue");
        try
        {
          await ScenarioAsync("Real OpenAI smoke test", async () =>
          {
            var id = await SubmitAsync("real-openai");
            await WaitPublishedAsync(id);
            ai = await environment.StartAiAsync(real: true);
            await environment.UntilAsync("Real OpenAI completion", async () =>
            {
                if (ai.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping.IsCancellationRequested)
                    throw new InvalidOperationException("Real OpenAI smoke stopped; provider calls are not retried.");
                return await AnalysisAsync(id) is not null;
            }, 180);
            var analysis = (await AnalysisAsync(id))!;
            var request = await EventAsync(id);
            Require(analysis.EventId == request.Id && analysis.TicketId == id, "Real provider durable identity");
            ValidateTicketAiAnalysisCore.Execute(JsonSerializer.Deserialize<TicketAiAnalysisResult>(analysis.ResultJson, Json)!);
            await WaitCompletedAsync(id, state.Acknowledgements);
            Require(environment.RealAnalyzer.Attempts == 1 && environment.RealAnalyzer.Successes == 1, "Exactly one real provider request");
            await StopAiAsync();
          });
        }
        catch (Exception exception) when (environment.RealAnalyzer.Unavailable || exception is OptionsValidationException)
        {
            result.Status = "BLOCKED";
            result.Detail = environment.RealAnalyzer.HttpStatus is { } status
                ? $"Provider unavailable or inaccessible (HTTP {status}); no retry was attempted."
                : "Provider configuration or network access unavailable; no retry was attempted.";
            await StopAiAsync();
        }
        finally
        {
            report.Evidence["RealOpenAiAttempts"] = environment.RealAnalyzer.Attempts.ToString();
            report.Evidence["RealOpenAiSuccessfulResponses"] = environment.RealAnalyzer.Successes.ToString();
        }
    }

    private async Task ScenarioAsync(string name, Func<Task> operation)
    {
        var scenario = report.Scenarios.Single(item => item.Name == name);
        scenario.Status = "RUNNING";
        Console.WriteLine($"Verifying: {name}");
        await SaveAsync();
        try { await operation(); scenario.Status = "PASS"; }
        catch (Exception exception)
        {
            scenario.Status = "FAIL";
            scenario.Detail = exception is VerificationException or VerificationStageTimeoutException ? exception.Message
                : $"Verification stopped with {exception.GetType().Name}; raw exception content is intentionally omitted.";
            throw;
        }
        finally { await SaveAsync(); }
    }

    private async Task<string> LoginAsync(string email)
    {
        using var response = await ApiAsync("/api/auth/login", new { email, password = environment.Password });
        Require(response.StatusCode == HttpStatusCode.OK, "Authentication workflow");
        using var json = await response.Content.ReadFromJsonAsync<JsonDocument>();
        return json!.RootElement.GetProperty("accessToken").GetString()!;
    }

    private async Task<string> CreateUserAsync(string administrator, string email, int role)
    {
        using var created = await ApiAsync("/api/users", new { firstName = "Verification", lastName = "User", email, role }, administrator);
        Require(created.StatusCode == HttpStatusCode.Created, "Create test user through API");
        using var user = await created.Content.ReadFromJsonAsync<JsonDocument>();
        var id = user!.RootElement.GetProperty("id").GetGuid();
        using var provisioned = await ApiAsync($"/api/users/{id}/access-provisioning", null, administrator);
        Require(provisioned.StatusCode == HttpStatusCode.Created, "Provision test-user access");
        using var provisioning = await provisioned.Content.ReadFromJsonAsync<JsonDocument>();
        using var activation = await ApiAsync("/api/auth/activate", new
        {
            activationToken = provisioning!.RootElement.GetProperty("activationToken").GetString(), password = environment.Password
        });
        Require(activation.StatusCode == HttpStatusCode.NoContent, "Activate test user");
        return await LoginAsync(email);
    }

    private async Task<Guid> SubmitAsync(string marker)
    {
        using var response = await ApiAsync("/api/tickets", new { title = Marker(marker), description = "Synthetic verification request. No customer data.", priority = 2 }, customerToken);
        Require(response.StatusCode == HttpStatusCode.Created, "Ticket HTTP response");
        using var json = await response.Content.ReadFromJsonAsync<JsonDocument>();
        return json!.RootElement.GetProperty("id").GetGuid();
    }

    private Task<HttpResponseMessage> ApiAsync(string path, object? body, string? token = null) => SendApiAsync(HttpMethod.Post, path, body, token);
    private Task<HttpResponseMessage> GetApiAsync(string path, string token) => SendApiAsync(HttpMethod.Get, path, null, token);
    private async Task<HttpResponseMessage> SendApiAsync(HttpMethod method, string path, object? body, string? token)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null) request.Content = JsonContent.Create(body);
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await environment.Api.SendAsync(request, environment.Deadline.Token);
    }

    private async Task<OutboxMessage> EventAsync(Guid id)
    {
        await using var database = environment.Database();
        var rows = await database.OutboxMessages.AsNoTracking().Where(row => row.Type == TicketAiAnalysisRequestedV1.Type).ToListAsync(environment.Deadline.Token);
        var matching = rows.Where(row => JsonSerializer.Deserialize<TicketAiAnalysisRequestedV1>(row.Payload)!.TicketId == id).ToArray();
        Require(matching.Length == 1, "Exactly one analysis Outbox event per ticket");
        return matching[0];
    }

    private async Task<TicketAiAnalysis?> AnalysisAsync(Guid id)
    {
        await using var database = environment.Database();
        return await database.TicketAiAnalyses.AsNoTracking().SingleOrDefaultAsync(row => row.TicketId == id, environment.Deadline.Token);
    }

    private async Task<string> SnapshotAsync(Guid id)
    {
        await using var database = environment.Database();
        var ticket = await database.Tickets.AsNoTracking().SingleAsync(ticket => ticket.Id == id);
        var history = await database.TicketHistories.AsNoTracking().Where(row => row.TicketId == id).OrderBy(row => row.Id).ToListAsync();
        return JsonSerializer.Serialize(new { ticket.Priority, ticket.Status, ticket.AssignedEmployeeUserId, ticket.UpdatedAt, ticket.ClosedAt, history });
    }

    private Task WaitPublishedAsync(Guid id, int seconds = 90) => environment.UntilAsync("Outbox publication", async () => (await EventAsync(id)).PublishedAt is not null, seconds);

    private Task WaitCompletedAsync(Guid id, long priorAcks) => environment.UntilAsync("Durable completion and acknowledgement", async () =>
    {
        var queue = await QueueAsync();
        return await AnalysisAsync(id) is not null && queue.Acknowledgements > priorAcks && queue.Ready == 0 && queue.Unacknowledged == 0;
    });

    private async Task WaitFailStopAsync(IHost host, Guid id, long priorAcks)
    {
        await environment.UntilAsync("AI fail-stop and retained delivery", async () =>
        {
            var state = await QueueAsync();
            return host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping.IsCancellationRequested
                && state.Ready == 1 && state.Unacknowledged == 0 && state.Consumers == 0;
        });
        Require(await AnalysisAsync(id) is null && (await QueueAsync()).Acknowledgements == priorAcks, "Failed processing must not complete or acknowledge");
    }

    private async Task StopAiAsync()
    {
        if (ai is not null) { await environment.StopHostAsync(ai); ai = null; }
    }

    private async Task<QueueState> QueueAsync(string? name = null)
    {
        var path = $"/api/queues/{Uri.EscapeDataString(environment.RunId)}/{Uri.EscapeDataString(name ?? RabbitMqOutboxRoute.AiAnalysis.QueueName)}";
        using var response = await environment.Broker.GetAsync(path, environment.Deadline.Token);
        Require(response.IsSuccessStatusCode, "Broker queue evidence");
        using var document = await response.Content.ReadFromJsonAsync<JsonDocument>();
        var root = document!.RootElement;
        var acknowledgements = root.TryGetProperty("message_stats", out var stats) && stats.TryGetProperty("ack", out var count) ? count.GetInt64() : 0;
        return new(root.TryGetProperty("messages_ready", out var ready) ? ready.GetInt64() : -1,
            root.TryGetProperty("messages_unacknowledged", out var unacked) ? unacked.GetInt64() : -1,
            acknowledgements, root.TryGetProperty("consumers", out var consumers) ? consumers.GetInt32() : -1);
    }

    internal static bool HasRecipient(JsonElement message, string recipient) =>
        message.GetProperty("To").EnumerateArray().Any(to =>
            string.Equals(to.GetProperty("Address").GetString(), recipient, StringComparison.OrdinalIgnoreCase));

    private string Marker(string marker) => environment.RunId + "-" + marker;
    private static void Require(bool condition, string stage)
    {
        if (!condition) throw new VerificationException(stage);
    }
    private async Task SaveAsync()
    {
        var temporary = environment.ReportPath + ".tmp";
        await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        await ReplaceReportAsync(temporary, environment.ReportPath);
    }

    internal static async Task ReplaceReportAsync(string temporary, string destination)
    {
        // A Windows report reader may briefly deny delete-sharing during atomic replacement.
        // Retry only the replacement, preserving the last complete report and a bounded wait.
        for (var attempt = 0; ; attempt++)
        {
            try { File.Move(temporary, destination, overwrite: true); return; }
            catch (Exception exception) when (attempt < 20 && exception is IOException or UnauthorizedAccessException)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50));
            }
        }
    }
    private sealed record QueueState(long Ready, long Unacknowledged, long Acknowledgements, int Consumers);
    private sealed class VerificationException(string stage) : Exception(stage);
}

internal sealed class RunReport(string runId)
{
    public string RunId { get; } = runId;
    public string Overall { get; set; } = "NOT RUN";
    public List<ScenarioResult> Scenarios { get; } = new[] { "Infrastructure", "Ticket creation", "Atomic Outbox", "RabbitMQ publication", "AI processing",
        "Durable result", "RabbitMQ acknowledgement", "Duplicate delivery", "Notification / Mailpit", "AI failure recovery", "Persistence failure",
        "Broker outage", "Worker shutdown", "Real OpenAI smoke test", "Cleanup" }.Select(name => new ScenarioResult(name)).ToList();
    public Dictionary<string, string> Evidence { get; } = new();
    public List<string> Diagnostics { get; } = [];
}

internal sealed class ScenarioResult(string name)
{
    public string Name { get; } = name;
    public string Status { get; set; } = "NOT RUN";
    public string Detail { get; set; } = "";
}
