using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NotificationService.Email;
using NotificationService.Messaging;
using NotificationService.Shell;
using NotificationService.Worker;
using RabbitMQ.Client;
using RabbitMq.Consumers;
using RabbitMq.Publishers;
using ServiceDesk.AiWorker;
using ServiceDesk.AI.AiAnalysis;
using ServiceDesk.AI.Configuration;
using ServiceDesk.Messaging;
using ServiceDesk.NotificationWorker;
using ServiceDesk.Repository;
using ServiceDesk.Shell.AiAnalysis;
using ServiceDesk.Shell.Notifications;

namespace ServiceDesk.LiveIntegration;

internal sealed class RunEnvironment : IAsyncDisposable
{
    public string RunId { get; } = Required("SDV_RUN_ID");
    public string Root { get; } = Required("SDV_REPO_ROOT");
    public string ReportPath { get; } = Required("SDV_REPORT_PATH");
    public bool RealOpenAi { get; } = bool.Parse(Required("SDV_REAL_OPENAI"));
    public CancellationTokenSource Deadline { get; } = new(TimeSpan.FromSeconds(int.Parse(Required("SDV_TIMEOUT_SECONDS"))));
    public string ConnectionString { get; }
    public IConfiguration Configuration { get; }
    public HttpClient Broker { get; }
    public HttpClient Mailpit { get; } = new() { BaseAddress = new Uri($"http://127.0.0.1:{Required("SDV_MAILPIT_PORT")}"), Timeout = TimeSpan.FromSeconds(10) };
    public HttpClient Api { get; } = new() { Timeout = TimeSpan.FromSeconds(20) };
    public ControlledAnalyzer Analyzer { get; } = new();
    public RealAnalyzerEvidence RealAnalyzer { get; } = new();
    public OutboxEvidence Outbox { get; } = new();
    public string CustomerEmail => $"customer-{RunId}@example.invalid";
    public string Password { get; } = Guid.NewGuid().ToString("N") + "-aA9!";
    private readonly List<IHost> hosts = [];
    private Process? apiProcess;
    private bool ownershipVerified;
    private readonly ConsoleCancelEventHandler cancelHandler;

    public RunEnvironment()
    {
        RunIsolation.Validate(RunId, Root, ReportPath, Required("SDV_COMPOSE_PATH"));
        cancelHandler = (_, args) => { args.Cancel = true; Deadline.Cancel(); };
        Console.CancelKeyPress += cancelHandler;
        ConnectionString = new SqlConnectionStringBuilder
        {
            DataSource = $"127.0.0.1,{Required("SDV_SQL_PORT")}",
            InitialCatalog = "ServiceDeskVerification_" + RunId,
            UserID = "sa", Password = Required("SDV_SQL_PASSWORD"),
            Encrypt = false, TrustServerCertificate = true, ConnectTimeout = 5
        }.ConnectionString;
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:ServiceDesk"] = ConnectionString,
            ["RabbitMq:HostName"] = "127.0.0.1", ["RabbitMq:Port"] = Required("SDV_AMQP_PORT"),
            ["RabbitMq:VirtualHost"] = RunId, ["RabbitMq:UserName"] = "verification",
            ["RabbitMq:Password"] = Required("SDV_BROKER_PASSWORD"),
            ["OpenAI:ApiKey"] = "deterministic-test-only",
            ["OpenAI:Model"] = "deterministic-test-only", ["OpenAI:BaseUrl"] = "https://api.openai.com/"
        };
        Configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        Broker = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{Required("SDV_MANAGEMENT_PORT")}"), Timeout = TimeSpan.FromSeconds(10) };
        Broker.DefaultRequestHeaders.Authorization = new("Basic", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("verification:" + Required("SDV_BROKER_PASSWORD"))));
    }

    public ServiceDeskDbContext Database()
    {
        if (!ownershipVerified) throw new InvalidOperationException("Verify Docker ownership before opening the test database.");
        return new(new DbContextOptionsBuilder<ServiceDeskDbContext>().UseSqlServer(ConnectionString).Options);
    }

    public ConnectionFactory Factory()
    {
        if (!ownershipVerified) throw new InvalidOperationException("Verify Docker ownership before opening AMQP transport.");
        return new()
        {
        HostName = "127.0.0.1", Port = int.Parse(Required("SDV_AMQP_PORT")), VirtualHost = RunId,
        UserName = "verification", Password = Required("SDV_BROKER_PASSWORD"),
        AutomaticRecoveryEnabled = false, DispatchConsumersAsync = true,
        RequestedConnectionTimeout = TimeSpan.FromSeconds(5)
        };
    }

    public async Task PrepareAsync()
    {
        // Verify Docker project labels AND mapped ports before any database connection or mutation.
        foreach (var service in new[] { "sql", "rabbitmq", "mailpit" })
        {
            var id = (await DockerAsync("ps", "--quiet", service)).Trim();
            if (!Regex.IsMatch(id, "^[0-9a-f]{12,64}$")) throw new InvalidOperationException("Expected exactly one owned container.");
            var label = await CommandAsync("docker", ["inspect", "--format", "{{index .Config.Labels \"com.docker.compose.project\"}}", id]);
            if (label.Trim() != RunId) throw new InvalidOperationException("Infrastructure ownership verification failed.");
        }
        foreach (var mapping in new[] { ("sql", 1433, "SDV_SQL_PORT"), ("rabbitmq", 5672, "SDV_AMQP_PORT"),
            ("rabbitmq", 15672, "SDV_MANAGEMENT_PORT"), ("mailpit", 1025, "SDV_SMTP_PORT"), ("mailpit", 8025, "SDV_MAILPIT_PORT") })
        {
            if ((await DockerAsync("port", mapping.Item1, mapping.Item2.ToString())).Trim() != "127.0.0.1:" + Required(mapping.Item3))
                throw new InvalidOperationException("Infrastructure port ownership verification failed.");
        }
        ownershipVerified = true;
        await UntilAsync("SQL readiness", async () =>
        {
            var master = new SqlConnectionStringBuilder(ConnectionString) { InitialCatalog = "master" };
            await using var connection = new SqlConnection(master.ConnectionString);
            await connection.OpenAsync(Deadline.Token);
            return true;
        }, 180, retryConnectionErrors: true);
        await using (var database = Database()) { await database.Database.MigrateAsync(Deadline.Token); }
        await UntilAsync("RabbitMQ readiness", async () =>
        {
            using var response = await Broker.GetAsync("/api/overview", Deadline.Token);
            return response.IsSuccessStatusCode;
        }, 120, retryConnectionErrors: true);
        await UntilAsync("Mailpit readiness", async () =>
        {
            using var response = await Mailpit.GetAsync("/api/v1/info", Deadline.Token);
            return response.IsSuccessStatusCode;
        }, 90, retryConnectionErrors: true);
    }

    public async Task StartApiAsync()
    {
        using (var bootstrap = StartApiProcess(["--bootstrap-administrator"]))
        {
            try { await bootstrap.WaitForExitAsync(Deadline.Token).WaitAsync(TimeSpan.FromSeconds(60)); }
            catch { if (!bootstrap.HasExited) bootstrap.Kill(entireProcessTree: true); throw; }
            if (bootstrap.ExitCode != 0) throw new InvalidOperationException("Administrator bootstrap failed.");
        }
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        Api.BaseAddress = new Uri($"http://127.0.0.1:{port}");
        apiProcess = StartApiProcess([], Api.BaseAddress.ToString());
        await UntilAsync("WebApi HTTP readiness", async () =>
        {
            if (apiProcess.HasExited) throw new InvalidOperationException("WebApi exited during startup.");
            using var response = await Api.GetAsync("/api/tickets", Deadline.Token);
            return response.StatusCode == HttpStatusCode.Unauthorized;
        }, 60, retryConnectionErrors: true);
    }

    private Process StartApiProcess(string[] arguments, string? url = null)
    {
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        start.ArgumentList.Add(Path.Combine(Root, "src", "ServiceDesk.WebApi", "bin", "Debug", "net10.0", "ServiceDesk.WebApi.dll"));
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        start.Environment["ConnectionStrings__ServiceDesk"] = ConnectionString;
        start.Environment["Jwt__Issuer"] = RunId;
        start.Environment["Jwt__Audience"] = RunId;
        start.Environment["Jwt__SigningKey"] = Password + Password;
        start.Environment["BootstrapAdministrator__FirstName"] = "Verification";
        start.Environment["BootstrapAdministrator__LastName"] = "Administrator";
        start.Environment["BootstrapAdministrator__Email"] = $"administrator-{RunId}@example.invalid";
        start.Environment["BootstrapAdministrator__Password"] = Password;
        start.Environment["Logging__LogLevel__Default"] = "None";
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
        if (url is not null) start.Environment["ASPNETCORE_URLS"] = url;
        var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start owned WebApi process.");
        // Never persist raw HTTP, EF, or authentication logs. Drain both pipes to avoid deadlocks.
        process.OutputDataReceived += (_, _) => { };
        process.ErrorDataReceived += (_, _) => { };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return process;
    }

    public async Task<IHost> StartAiAsync(bool real = false)
    {
        var builder = Builder();
        if (real)
        {
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenAI:ApiKey"] = Environment.GetEnvironmentVariable("OpenAI__ApiKey"),
                ["OpenAI:Model"] = Environment.GetEnvironmentVariable("OpenAI__Model"),
                ["OpenAI:BaseUrl"] = Environment.GetEnvironmentVariable("OpenAI__BaseUrl") ?? "https://api.openai.com/"
            });
        }
        builder.Services.AddAiWorker(builder.Configuration);
        if (!real)
        {
            builder.Services.RemoveAll<ITicketAiAnalyzer>();
            builder.Services.AddScoped<ITicketAiAnalyzer>(_ => Analyzer);
        }
        else
        {
            builder.Services.RemoveAll<ITicketAiAnalyzer>();
            builder.Services.AddHttpClient<OpenAiTicketAnalyzer>((services, client) =>
                client.BaseAddress = new Uri(services.GetRequiredService<IOptions<OpenAiOptions>>().Value.BaseUrl));
            builder.Services.AddScoped<ITicketAiAnalyzer>(services =>
                new ObservedRealAnalyzer(services.GetRequiredService<OpenAiTicketAnalyzer>(), RealAnalyzer));
        }
        return await StartHostAsync(builder);
    }

    public async Task<IHost> StartOutboxAsync()
    {
        var builder = Builder();
        builder.Services.AddServiceDeskRepository(builder.Configuration);
        builder.Services.AddSingleton<IConnectionFactory>(_ => Factory());
        builder.Services.AddSingleton(new RabbitMqPublisherOptions());
        builder.Services.AddSingleton<IRabbitMqPublisher, RabbitMqPublisher>();
        builder.Services.AddSingleton(new RabbitMqOutboxRouting([RabbitMqOutboxRoute.Notification, RabbitMqOutboxRoute.AiAnalysis]));
        builder.Services.AddSingleton<RabbitMqOutboxMessagePublisher>();
        builder.Services.AddSingleton<IOutboxMessagePublisher>(services =>
            new ObservedOutboxPublisher(services.GetRequiredService<RabbitMqOutboxMessagePublisher>(), Outbox));
        builder.Services.AddScoped<ProcessOutboxShell>();
        builder.Services.AddHostedService<OutboxPublisherWorker>();
        return await StartHostAsync(builder);
    }

    public async Task<IHost> StartNotificationsAsync()
    {
        var builder = Builder();
        builder.Services.AddSingleton(new SmtpOptions { Host = "127.0.0.1", Port = int.Parse(Required("SDV_SMTP_PORT")), FromAddress = "verification@example.invalid", EnableSsl = false });
        builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
        builder.Services.AddScoped<ProcessNotificationShell>();
        builder.Services.AddScoped<NotificationMessageHandler>();
        builder.Services.AddSingleton<IConnectionFactory>(_ => Factory());
        builder.Services.AddSingleton(NotificationMessageHandler.ConsumerOptions);
        builder.Services.AddSingleton<RabbitMqConsumer<NotificationMessageHandler>>();
        builder.Services.AddHostedService<NotificationConsumerWorker>();
        return await StartHostAsync(builder);
    }

    private HostApplicationBuilder Builder()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        builder.Configuration.AddConfiguration(Configuration);
        builder.Logging.ClearProviders();
        builder.Services.Configure<HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromSeconds(45));
        return builder;
    }

    private async Task<IHost> StartHostAsync(HostApplicationBuilder builder)
    {
        var host = builder.Build();
        hosts.Add(host);
        await host.StartAsync(Deadline.Token).WaitAsync(TimeSpan.FromSeconds(45));
        return host;
    }

    public async Task StopHostAsync(IHost host)
    {
        await host.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(60));
        hosts.Remove(host);
        if (host is IAsyncDisposable asyncDisposable) await asyncDisposable.DisposeAsync();
        else host.Dispose();
    }

    public Task<string> DockerAsync(params string[] arguments) => CommandAsync("docker",
        new[] { "compose", "--project-name", RunId, "--file", Required("SDV_COMPOSE_PATH") }.Concat(arguments).ToArray());

    private async Task<string> CommandAsync(string command, string[] arguments)
    {
        var start = new ProcessStartInfo(command) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start owned infrastructure command.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        try { await process.WaitForExitAsync(Deadline.Token).WaitAsync(TimeSpan.FromSeconds(90)); }
        catch { if (!process.HasExited) process.Kill(entireProcessTree: true); throw; }
        await error; // Discard raw infrastructure errors; they may contain configuration values.
        if (process.ExitCode != 0) throw new InvalidOperationException("Owned infrastructure operation failed.");
        return await output;
    }

    public async Task UntilAsync(string stage, Func<Task<bool>> predicate, int seconds = 90, bool retryConnectionErrors = false)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Deadline.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(seconds));
        try
        {
            while (true)
            {
                timeout.Token.ThrowIfCancellationRequested();
                try { if (await predicate().WaitAsync(timeout.Token)) return; }
                catch (Exception exception) when (retryConnectionErrors && IsTransientReadinessFailure(exception, timeout.Token)) { }
                await Task.Delay(TimeSpan.FromMilliseconds(250), timeout.Token);
            }
        }
        catch (OperationCanceledException) { throw new VerificationStageTimeoutException(stage); }
    }

    internal static bool IsTransientReadinessFailure(Exception exception, CancellationToken cancellationToken) =>
        !cancellationToken.IsCancellationRequested && exception is SqlException or HttpRequestException or TaskCanceledException;

    public static string Required(string name) => Environment.GetEnvironmentVariable(name)
        ?? throw new InvalidOperationException("Run configuration is missing. Use the PowerShell entry point.");

    public async ValueTask DisposeAsync()
    {
        List<Exception> failures = [];
        foreach (var host in hosts.ToArray().Reverse())
        {
            try { await StopHostAsync(host); }
            catch (Exception exception) { failures.Add(exception); }
        }
        if (apiProcess is not null)
        {
            if (!apiProcess.HasExited) apiProcess.Kill(entireProcessTree: true);
            await apiProcess.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
            apiProcess.Dispose();
        }
        Console.CancelKeyPress -= cancelHandler;
        Api.Dispose(); Broker.Dispose(); Mailpit.Dispose(); Deadline.Dispose();
        if (failures.Count > 0) throw new AggregateException("Owned host cleanup failed.", failures);
    }
}

internal sealed class VerificationStageTimeoutException(string stage)
    : TimeoutException($"Verification timed out during {stage}.");
