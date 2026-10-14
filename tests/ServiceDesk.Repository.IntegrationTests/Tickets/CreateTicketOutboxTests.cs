using System.Text.Json;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Notification.Contracts;
using ServiceDesk.Contracts;
using ServiceDesk.Core.Tickets;
using ServiceDesk.Core.Users;
using ServiceDesk.Repository.Notifications;
using ServiceDesk.Repository.Tickets;
using ServiceDesk.Repository.Users;
using ServiceDesk.Shell.Notifications;
using ServiceDesk.Shell.Tickets;

namespace ServiceDesk.Repository.IntegrationTests.Tickets;

public sealed class CreateTicketOutboxTests : IAsyncLifetime
{
    private readonly SqliteConnection connection =
        new("Data Source=:memory:");

    private DbContextOptions<ServiceDeskDbContext> options = null!;

    public async Task InitializeAsync()
    {
        await connection.OpenAsync();

        options = new DbContextOptionsBuilder<ServiceDeskDbContext>()
            .UseSqlite(connection)
            .Options;

        await using ServiceDeskDbContext context = new(options);
        await context.Database.EnsureCreatedAsync();
    }

    public Task DisposeAsync() => connection.DisposeAsync().AsTask();

    [Fact]
    public async Task ExecuteAsync_WithValidCustomer_CommitsTicketHistoryAndAnalysisRequest()
    {
        // Arrange
        User customer = await seedCustomerAsync();
        CreateTicketCommand command = createCommand();

        await using ServiceDeskDbContext context = new(options);
        UserRepository userRepository = new(context);
        TicketRepository ticketRepository = new(context);
        CreateTicketShell shell = new(userRepository, ticketRepository);

        // Act
        Ticket returnedTicket = await shell.ExecuteAsync(command, customer.Id);

        await using ServiceDeskDbContext verification = new(options);
        Ticket storedTicket = await verification.Tickets
            .Include(ticket => ticket.History)
            .SingleAsync();

        OutboxMessage storedMessage = await verification.OutboxMessages
            .SingleAsync();

        TicketHistory storedHistory = storedTicket.History.Single();

        TicketAiAnalysisRequestedV1? analysisRequested =
            JsonSerializer.Deserialize<TicketAiAnalysisRequestedV1>(
                storedMessage.Payload);

        TicketAiAnalysisRequestedV1 expectedEvent = new(
            storedMessage.Id,
            returnedTicket.Id,
            returnedTicket.CreatedAt);

        // Assert
        returnedTicket.Id.Should().NotBeEmpty();
        storedTicket.Id.Should().Be(returnedTicket.Id);
        storedTicket.CustomerUserId.Should().Be(customer.Id);
        storedTicket.Title.Should().Be(command.Title);
        storedTicket.Description.Should().Be(command.Description);
        storedTicket.Priority.Should().Be(command.Priority);
        storedTicket.Status.Should().Be(TicketStatus.Open);

        storedTicket.History.Should().ContainSingle();
        storedHistory.Action.Should().Be(TicketHistoryAction.Created);
        storedHistory.TicketId.Should().Be(returnedTicket.Id);

        storedMessage.Id.Should().NotBeEmpty();
        storedMessage.Type.Should().Be(TicketAiAnalysisRequestedV1.Type);
        storedMessage.OccurredAt.Should().Be(returnedTicket.CreatedAt);
        storedMessage.PublishedAt.Should().BeNull();
        storedMessage.ClaimToken.Should().BeNull();
        storedMessage.ClaimExpiresAt.Should().BeNull();
        analysisRequested.Should().Be(expectedEvent);
    }

    [Theory]
    [InlineData("Tickets")]
    [InlineData("TicketHistories")]
    [InlineData("OutboxMessages")]
    public async Task ExecuteAsync_WhenAnyInsertFails_RollsBackEntireSubmission(
        string rejectedTable)
    {
        // Arrange
        User customer = await seedCustomerAsync();
        CreateTicketCommand command = createCommand();

        await using ServiceDeskDbContext context = new(options);

        // Table names come exclusively from the fixed InlineData above.
        string triggerSql = $"""
            CREATE TRIGGER reject_submission
            BEFORE INSERT ON "{rejectedTable}"
            BEGIN
                SELECT RAISE(ABORT, 'Forced submission persistence failure');
            END;
            """;

        await context.Database.ExecuteSqlRawAsync(triggerSql);

        UserRepository userRepository = new(context);
        TicketRepository ticketRepository = new(context);
        CreateTicketShell shell = new(userRepository, ticketRepository);

        Func<Task> operation = () =>
            shell.ExecuteAsync(command, customer.Id);

        // Act
        Exception? exception = await Record.ExceptionAsync(operation);

        await using ServiceDeskDbContext verification = new(options);
        int ticketCount = await verification.Tickets.CountAsync();
        int historyCount = await verification.TicketHistories.CountAsync();
        int outboxCount = await verification.OutboxMessages.CountAsync();
        int customerCount = await verification.Users.CountAsync();

        // Assert
        exception.Should().BeOfType<DbUpdateException>();
        ticketCount.Should().Be(0);
        historyCount.Should().Be(0);
        outboxCount.Should().Be(0);
        customerCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidTicket_DoesNotPersistTicketOrAnalysisRequest()
    {
        // Arrange
        User customer = await seedCustomerAsync();

        CreateTicketCommand command = new(
            " ",
            "The VPN rejects my credentials.",
            TicketPriority.High);

        await using ServiceDeskDbContext context = new(options);
        UserRepository userRepository = new(context);
        TicketRepository ticketRepository = new(context);
        CreateTicketShell shell = new(userRepository, ticketRepository);

        Func<Task> operation = () =>
            shell.ExecuteAsync(command, customer.Id);

        // Act
        Exception? exception = await Record.ExceptionAsync(operation);

        await using ServiceDeskDbContext verification = new(options);
        int ticketCount = await verification.Tickets.CountAsync();
        int historyCount = await verification.TicketHistories.CountAsync();
        int outboxCount = await verification.OutboxMessages.CountAsync();

        // Assert
        exception.Should().BeOfType<CreateTicketException>()
            .Which.Failure.Should().Be(CreateTicketFailureKind.InvalidTitle);

        ticketCount.Should().Be(0);
        historyCount.Should().Be(0);
        outboxCount.Should().Be(0);
    }

    [Fact]
    public async Task TryClaimNextAsync_WithOnlyAnalysisRequest_LeavesItPending()
    {
        // Arrange
        User customer = await seedCustomerAsync();
        CreateTicketCommand command = createCommand();

        await using (ServiceDeskDbContext submissionContext = new(options))
        {
            UserRepository users = new(submissionContext);
            TicketRepository tickets = new(submissionContext);
            CreateTicketShell shell = new(users, tickets);

            await shell.ExecuteAsync(command, customer.Id);
        }

        await using ServiceDeskDbContext context = new(options);
        OutboxRepository repository = new(context);
        Guid claimToken = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        // Act
        OutboxMessageToPublish? claimed = await repository.TryClaimNextAsync(
            claimToken,
            now,
            now.AddMinutes(5));

        await using ServiceDeskDbContext verification = new(options);
        OutboxMessage storedMessage = await verification.OutboxMessages
            .SingleAsync();

        // Assert
        claimed.Should().BeNull();
        storedMessage.Type.Should().Be(TicketAiAnalysisRequestedV1.Type);
        storedMessage.PublishedAt.Should().BeNull();
        storedMessage.ClaimToken.Should().BeNull();
        storedMessage.ClaimExpiresAt.Should().BeNull();
    }

    [Fact]
    public async Task TryClaimNextAsync_WithMixedEvents_ClaimsNotificationAndRetainsAnalysisRequest()
    {
        // Arrange
        User customer = await seedCustomerAsync();
        CreateTicketCommand command = createCommand();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Guid notificationId = Guid.NewGuid();

        NotificationRequestedV1 notification = new(
            "servicedesk",
            notificationId.ToString(),
            customer.Email,
            "Test notification",
            "Notification routing remains available.");

        OutboxMessage notificationMessage = new(
            notificationId,
            NotificationRequestedV1.Type,
            JsonSerializer.Serialize(notification),
            now,
            now);

        await using (ServiceDeskDbContext submissionContext = new(options))
        {
            UserRepository users = new(submissionContext);
            TicketRepository tickets = new(submissionContext);
            CreateTicketShell shell = new(users, tickets);

            await shell.ExecuteAsync(command, customer.Id);

            submissionContext.OutboxMessages.Add(notificationMessage);
            await submissionContext.SaveChangesAsync();
        }

        await using ServiceDeskDbContext context = new(options);
        OutboxRepository repository = new(context);
        Guid claimToken = Guid.NewGuid();

        // Act
        OutboxMessageToPublish? claimed = await repository.TryClaimNextAsync(
            claimToken,
            now,
            now.AddMinutes(5));

        await using ServiceDeskDbContext verification = new(options);
        OutboxMessage analysisMessage = await verification.OutboxMessages
            .SingleAsync(message =>
                message.Type == TicketAiAnalysisRequestedV1.Type);

        int outboxCount = await verification.OutboxMessages.CountAsync();

        // Assert
        claimed.Should().NotBeNull();
        claimed!.Id.Should().Be(notificationId);
        claimed.Type.Should().Be(NotificationRequestedV1.Type);
        outboxCount.Should().Be(2);
        analysisMessage.PublishedAt.Should().BeNull();
        analysisMessage.ClaimToken.Should().BeNull();
        analysisMessage.ClaimExpiresAt.Should().BeNull();
    }

    private async Task<User> seedCustomerAsync()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        User customer = new(
            Guid.NewGuid(),
            "Ada",
            "Lovelace",
            "ADA@EXAMPLE.COM",
            UserRole.Customer,
            true,
            now,
            now);

        await using ServiceDeskDbContext context = new(options);
        context.Users.Add(customer);
        await context.SaveChangesAsync();

        return customer;
    }

    private static CreateTicketCommand createCommand() => new(
        "Cannot access VPN",
        "The VPN rejects my credentials.",
        TicketPriority.High);
}