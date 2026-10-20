using System.Text.Json;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Notification.Contracts;
using NotificationService.Messaging;
using NotificationService.Shell;
using NSubstitute;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMq.Consumers;
using RabbitMq.Publishers;
using ServiceDesk.Contracts;
using ServiceDesk.Repository;
using ServiceDesk.Repository.Notifications;
using ServiceDesk.Shell.AiAnalysis;
using ServiceDesk.Shell.Notifications;
using ServiceDesk.Shell.Tickets;

namespace ServiceDesk.Messaging.Tests;

public sealed class PublisherCompatibilityTests
{
    [Fact]
    public async Task PublishedNotification_IsAcceptedByExistingHandler()
    {
        // Arrange
        await using Publication transport = new();
        IEmailSender sender = Substitute.For<IEmailSender>();
        NotificationMessageHandler handler = new(new ProcessNotificationShell(sender));
        NotificationRequestedV1 notification = new("servicedesk", "event-id", "customer@example.com", "Subject", "Body");
        OutboxMessageToPublish message = new(Guid.NewGuid(), NotificationRequestedV1.Type, JsonSerializer.Serialize(notification));

        // Act
        await transport.Adapter.PublishAsync(message);
        await handler.HandleAsync(transport.Delivery, CancellationToken.None);

        // Assert
        await sender.Received(1).SendAsync("customer@example.com", "Subject", "Body", CancellationToken.None);
        transport.Delivery.Exchange.Should().Be(NotificationMessageHandler.ConsumerOptions.Exchange);
        transport.Channel.Received(1).QueueBind(NotificationMessageHandler.ConsumerOptions.Queue,
            NotificationMessageHandler.ConsumerOptions.Exchange, NotificationRequestedV1.Type, null);
    }

    [Fact]
    public async Task PublishedAiRequest_IsAcceptedAndPreservesEventIdDeduplication()
    {
        // Arrange
        await using Publication transport = new();
        ITicketAiAnalysisRepository repository = Substitute.For<ITicketAiAnalysisRepository>();
        ITicketAiAnalyzer analyzer = Substitute.For<ITicketAiAnalyzer>();
        ITicketRepository tickets = Substitute.For<ITicketRepository>();
        TicketAiAnalysisRequestedV1 request = new(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);
        repository.IsCompletedAsync(request.Id, request.TicketId, CancellationToken.None).Returns(Task.FromResult(true));
        TicketAiAnalysisMessageHandler handler = new(new ProcessTicketAiAnalysisShell(repository, new AnalyzeRequestShell(tickets, analyzer)));
        OutboxMessageToPublish message = new(request.Id, TicketAiAnalysisRequestedV1.Type, JsonSerializer.Serialize(request));

        // Act
        await transport.Adapter.PublishAsync(message);
        await handler.HandleAsync(transport.Delivery, CancellationToken.None);
        await handler.HandleAsync(transport.Delivery with { Redelivered = true }, CancellationToken.None);

        // Assert
        await repository.Received(2).IsCompletedAsync(request.Id, request.TicketId, CancellationToken.None);
        await analyzer.DidNotReceive().AnalyzeAsync(Arg.Any<ServiceDesk.Core.AiAnalysis.TicketAiAnalysisInput>(), Arg.Any<CancellationToken>());
        transport.Delivery.MessageId.Should().Be(request.Id.ToString());
        transport.Channel.Received(1).QueueBind(TicketAiAnalysisMessageHandler.ConsumerOptions.Queue,
            TicketAiAnalysisMessageHandler.ConsumerOptions.Exchange, TicketAiAnalysisRequestedV1.Type, null);
    }

    [Theory]
    [InlineData("return")]
    [InlineData("nack")]
    [InlineData("timeout")]
    [InlineData("connection-lost")]
    [InlineData("cancellation")]
    public async Task PublicationFailure_LeavesStoredOutboxUnpublishedAndReclaimableAfterLease(string failureMode)
    {
        // Arrange
        await using SqliteConnection database = new("Data Source=:memory:");
        await database.OpenAsync();
        DbContextOptions<ServiceDeskDbContext> options = new DbContextOptionsBuilder<ServiceDeskDbContext>().UseSqlite(database).Options;
        await using ServiceDeskDbContext context = new(options);
        await context.Database.EnsureCreatedAsync();
        OutboxMessage pending = new(Guid.NewGuid(), TicketAiAnalysisRequestedV1.Type, "{}", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        context.OutboxMessages.Add(pending);
        await context.SaveChangesAsync();
        await using Publication transport = new();
        using CancellationTokenSource cancellation = new();
        transport.Channel.When(channel => channel.WaitForConfirmsOrDie(TimeSpan.FromSeconds(10))).Do(_ =>
        {
            if (failureMode == "return")
            {
                transport.Channel.BasicReturn += Raise.Event<EventHandler<BasicReturnEventArgs>>(transport.Channel, new BasicReturnEventArgs());
            }
            else if (failureMode == "cancellation") cancellation.Cancel();
            else throw new IOException(failureMode);
        });
        ProcessOutboxShell shell = new(new OutboxRepository(context), transport.Adapter);

        // Act
        Exception? failure = await Record.ExceptionAsync(() => shell.ProcessNextAsync(cancellation.Token));
        await using ServiceDeskDbContext verification = new(options);
        OutboxMessage stored = await verification.OutboxMessages.AsNoTracking().SingleAsync();
        OutboxRepository repository = new(verification);
        OutboxMessageToPublish? beforeExpiry = await repository.TryClaimNextAsync(Guid.NewGuid(), stored.ClaimExpiresAt!.Value.AddTicks(-1), stored.ClaimExpiresAt.Value.AddMinutes(5));
        OutboxMessageToPublish? afterExpiry = await repository.TryClaimNextAsync(Guid.NewGuid(), stored.ClaimExpiresAt.Value.AddTicks(1), stored.ClaimExpiresAt.Value.AddMinutes(5));

        // Assert
        failure.Should().NotBeNull();
        stored.PublishedAt.Should().BeNull();
        beforeExpiry.Should().BeNull();
        afterExpiry.Should().NotBeNull();
        afterExpiry!.Id.Should().Be(pending.Id);
    }

    [Fact]
    public async Task ConfirmedPublication_DatabaseMarkFailureAllowsDuplicateWithSameIdentity()
    {
        // Arrange
        await using Publication transport = new();
        IOutboxRepository repository = Substitute.For<IOutboxRepository>();
        OutboxMessageToPublish message = new(Guid.NewGuid(), TicketAiAnalysisRequestedV1.Type, "{}");
        repository.TryClaimNextAsync(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<OutboxMessageToPublish?>(message));
        repository.MarkPublishedAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<bool>(new IOException("Database unavailable")), Task.FromResult(true));
        ProcessOutboxShell shell = new(repository, transport.Adapter);

        // Act
        Exception? failure = await Record.ExceptionAsync(() => shell.ProcessNextAsync());
        string? firstIdentity = transport.Delivery.MessageId;
        bool retried = await shell.ProcessNextAsync(); // Repository grants the next eligible lease.

        // Assert
        failure.Should().BeOfType<IOException>();
        retried.Should().BeTrue();
        transport.Delivery.MessageId.Should().Be(firstIdentity).And.Be(message.Id.ToString());
        transport.Channel.Received(2).WaitForConfirmsOrDie(TimeSpan.FromSeconds(10));
    }

    private sealed class Publication : IAsyncDisposable
    {
        public IModel Channel { get; } = Substitute.For<IModel>();
        public RabbitMqPublisher Publisher { get; }
        public RabbitMqOutboxMessagePublisher Adapter { get; }
        public MessageContext Delivery { get; private set; } = null!;

        public Publication()
        {
            IConnectionFactory factory = Substitute.For<IConnectionFactory>();
            IConnection connection = Substitute.For<IConnection>();
            IBasicProperties properties = Substitute.For<IBasicProperties>();
            factory.CreateConnection().Returns(connection);
            connection.IsOpen.Returns(true);
            connection.CreateModel().Returns(Channel);
            Channel.IsOpen.Returns(true);
            Channel.CreateBasicProperties().Returns(properties);
            Channel.When(channel => channel.BasicPublish(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(),
                Arg.Any<IBasicProperties>(), Arg.Any<ReadOnlyMemory<byte>>())).Do(call =>
            {
                Delivery = new(call.ArgAt<ReadOnlyMemory<byte>>(4).ToArray(), call.ArgAt<string>(0), call.ArgAt<string>(1),
                    properties.MessageId, properties.Type, properties.ContentType, false);
            });
            Publisher = new(factory, new());
            Adapter = new(Publisher, new([RabbitMqOutboxRoute.Notification, RabbitMqOutboxRoute.AiAnalysis]));
        }

        public ValueTask DisposeAsync() => Publisher.DisposeAsync();
    }
}
