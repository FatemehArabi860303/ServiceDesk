using System.Text.Json;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Notification.Contracts;
using ServiceDesk.Core.Tickets;
using ServiceDesk.Core.Users;
using ServiceDesk.Repository;
using ServiceDesk.Repository.Tickets;
using ServiceDesk.Shell.Notifications;

namespace ServiceDesk.Repository.IntegrationTests.Tickets;

public sealed class ResolveTicketRepositoryTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private DbContextOptions<ServiceDeskDbContext> options = null!;

    public async Task InitializeAsync()
    {
        await connection.OpenAsync();
        options = new DbContextOptionsBuilder<ServiceDeskDbContext>().UseSqlite(connection).Options;
        await using var context = new ServiceDeskDbContext(options);
        await context.Database.EnsureCreatedAsync();
    }

    public Task DisposeAsync() => connection.DisposeAsync().AsTask();

    [Fact]
    public async Task TryResolveAsync_PersistsTicketHistoryAndCustomerNotification()
    {
        // Arrange
        var (ticket, customer) = await SeedAsync();
        await using var context = new ServiceDeskDbContext(options);
        var historyId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var notification = new TicketProgressNotification(Guid.NewGuid(),
            ResolveTicketCore.Execute(ticket, ticket.AssignedEmployeeUserId!.Value, UserRole.Employee, historyId, now), now);
        var repository = new TicketRepository(context);

        // Act
        var resolved = await repository.TryResolveAsync(ticket, historyId, notification);

        // Assert
        resolved.Should().BeTrue();
        await using var verification = new ServiceDeskDbContext(options);
        var stored = await verification.Tickets.Include(t => t.History).SingleAsync();
        stored.Status.Should().Be(TicketStatus.Resolved);
        stored.AssignedEmployeeUserId.Should().Be(ticket.AssignedEmployeeUserId);
        stored.CreatedAt.Should().Be(ticket.CreatedAt);
        stored.UpdatedAt.Should().Be(now);
        var history = stored.History.Single(h => h.Action == TicketHistoryAction.Resolved);
        history.ActorUserId.Should().Be(ticket.AssignedEmployeeUserId!.Value);
        history.OccurredAt.Should().Be(now);
        var outbox = await verification.OutboxMessages.SingleAsync();
        outbox.Id.Should().Be(notification.EventId);
        outbox.PublishedAt.Should().BeNull();
        outbox.Type.Should().Be(NotificationRequestedV1.Type);
        JsonSerializer.Deserialize<NotificationRequestedV1>(outbox.Payload).Should().Be(new NotificationRequestedV1(
            "servicedesk", notification.EventId.ToString(), customer.Email,
            "Your support request has been resolved",
            "Your support request has been marked as resolved. Please review the outcome."));
    }

    [Fact]
    public async Task TryResolveAsync_WithCompetingSnapshots_OnlyWinnerPersistsHistoryAndOutbox()
    {
        // Arrange
        var (ticket, _) = await SeedAsync();
        await using var firstContext = new ServiceDeskDbContext(options);
        await using var secondContext = new ServiceDeskDbContext(options);
        var firstRepository = new TicketRepository(firstContext);
        var secondRepository = new TicketRepository(secondContext);
        var first = (await firstRepository.GetByIdAsync(ticket.Id))!;
        var second = (await secondRepository.GetByIdAsync(ticket.Id))!;
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var firstNotification = Resolve(first, firstId);
        var secondNotification = Resolve(second, secondId);

        // Act
        var won = await firstRepository.TryResolveAsync(first, firstId, firstNotification);
        var lost = await secondRepository.TryResolveAsync(second, secondId, secondNotification);

        // Assert
        won.Should().BeTrue();
        lost.Should().BeFalse();
        await using var verification = new ServiceDeskDbContext(options);
        (await verification.TicketHistories.CountAsync(h => h.Action == TicketHistoryAction.Resolved)).Should().Be(1);
        (await verification.OutboxMessages.SingleAsync()).Id.Should().Be(firstNotification.EventId);
    }

    [Fact]
    public async Task TryResolveAsync_WhenHistoryInsertFails_RollsBackTicketAndOutbox()
    {
        // Arrange
        var (ticket, _) = await SeedAsync();
        await using var context = new ServiceDeskDbContext(options);
        var repository = new TicketRepository(context);
        var progressed = ResolveTicketCore.Execute(ticket, ticket.AssignedEmployeeUserId!.Value,
            UserRole.Employee, Guid.NewGuid(), DateTimeOffset.UtcNow);
        var resolvedHistory = ticket.History.Last();
        // Force a database failure after the conditional update and outbox creation.
        await context.Database.ExecuteSqlRawAsync("CREATE TRIGGER reject_resolution BEFORE INSERT ON TicketHistories WHEN NEW.Action = 'Resolved' BEGIN SELECT RAISE(ABORT, 'test failure'); END;");
        var notification = new TicketProgressNotification(Guid.NewGuid(), progressed, progressed.OccurredAt);

        // Act
        Func<Task> act = () => repository.TryResolveAsync(ticket, resolvedHistory.Id, notification);

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();
        await using var verification = new ServiceDeskDbContext(options);
        (await verification.Tickets.SingleAsync()).Status.Should().Be(TicketStatus.InProgress);
        (await verification.TicketHistories.CountAsync()).Should().Be(3);
        (await verification.OutboxMessages.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task TryResolveAsync_WhenAssignmentChanged_RejectsStaleResolution()
    {
        // Arrange
        var (ticket, customer) = await SeedAsync();
        var historyId = Guid.NewGuid();
        var notification = Resolve(ticket, historyId);
        await using var context = new ServiceDeskDbContext(options);
        await context.Tickets.ExecuteUpdateAsync(setters => setters.SetProperty(t => t.AssignedEmployeeUserId, customer.Id));
        var repository = new TicketRepository(context);

        // Act
        var resolved = await repository.TryResolveAsync(ticket, historyId, notification);

        // Assert
        resolved.Should().BeFalse();
        (await context.Tickets.SingleAsync()).Status.Should().Be(TicketStatus.InProgress);
        (await context.TicketHistories.CountAsync()).Should().Be(3);
        (await context.OutboxMessages.CountAsync()).Should().Be(0);
    }

    private async Task<(Ticket Ticket, User Customer)> SeedAsync()
    {
        var now = DateTimeOffset.UtcNow;
        var customer = new User(Guid.NewGuid(), "Ada", "Customer", "CUSTOMER@EXAMPLE.COM", UserRole.Customer, true, now, now);
        var employee = new User(Guid.NewGuid(), "Ada", "Employee", "EMPLOYEE@EXAMPLE.COM", UserRole.Employee, true, now, now);
        var ticket = CreateTicketCore.Execute(new CreateTicketCommand("VPN", "Cannot connect", TicketPriority.High),
            new CreateTicketFacts(true), customer.Id, Guid.NewGuid(), Guid.NewGuid(), now);
        AssignTicketCore.Execute(ticket, employee.Id, Guid.NewGuid(), now);
        StartWorkCore.Execute(ticket, employee.Id, Guid.NewGuid(), now);
        await using var context = new ServiceDeskDbContext(options);
        context.Users.AddRange(customer, employee);
        context.Tickets.Add(ticket);
        await context.SaveChangesAsync();
        return (ticket, customer);
    }

    private static TicketProgressNotification Resolve(Ticket ticket, Guid historyId)
    {
        var now = DateTimeOffset.UtcNow;
        return new TicketProgressNotification(Guid.NewGuid(), ResolveTicketCore.Execute(ticket,
            ticket.AssignedEmployeeUserId!.Value, UserRole.Employee, historyId, now), now);
    }
}
