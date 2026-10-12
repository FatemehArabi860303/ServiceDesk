using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Core.Tickets;
using ServiceDesk.Core.Users;
using ServiceDesk.Repository;
using ServiceDesk.Repository.Tickets;

namespace ServiceDesk.Repository.IntegrationTests.Tickets;

public sealed class ConfirmResolutionRepositoryTests : IAsyncLifetime
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
    public async Task TryConfirmResolutionAsync_PersistsClosureAndHistoryWithoutNotification()
    {
        // Arrange
        var ticket = await SeedAsync();
        await using var context = new ServiceDeskDbContext(options);
        var historyId = Guid.NewGuid();
        var now = ticket.UpdatedAt.AddHours(1);
        ConfirmResolutionCore.Execute(ticket, ticket.CustomerUserId, UserRole.Customer, historyId, now);
        var repository = new TicketRepository(context);

        // Act
        var confirmed = await repository.TryConfirmResolutionAsync(ticket, historyId);

        // Assert
        confirmed.Should().BeTrue();
        await using var verification = new ServiceDeskDbContext(options);
        var stored = await verification.Tickets.Include(t => t.History).SingleAsync();
        stored.Status.Should().Be(TicketStatus.Closed);
        stored.ClosedAt.Should().Be(now);
        stored.UpdatedAt.Should().Be(now);
        stored.CreatedAt.Should().Be(ticket.CreatedAt);
        stored.CustomerUserId.Should().Be(ticket.CustomerUserId);
        stored.AssignedEmployeeUserId.Should().Be(ticket.AssignedEmployeeUserId);
        stored.History.Should().HaveCount(5);
        stored.History.Single(h => h.Action == TicketHistoryAction.Closed).Should()
            .Be(new TicketHistory(historyId, ticket.Id, ticket.CustomerUserId, TicketHistoryAction.Closed, now));
        (await verification.OutboxMessages.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task TryConfirmResolutionAsync_WithCompetingSnapshots_OnlyWinnerPersistsHistory()
    {
        // Arrange
        var ticket = await SeedAsync();
        await using var firstContext = new ServiceDeskDbContext(options);
        await using var secondContext = new ServiceDeskDbContext(options);
        var firstRepository = new TicketRepository(firstContext);
        var secondRepository = new TicketRepository(secondContext);
        var first = (await firstRepository.GetByIdAsync(ticket.Id))!;
        var second = (await secondRepository.GetByIdAsync(ticket.Id))!;
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var now = ticket.UpdatedAt.AddHours(1);
        ConfirmResolutionCore.Execute(first, ticket.CustomerUserId, UserRole.Customer, firstId, now);
        ConfirmResolutionCore.Execute(second, ticket.CustomerUserId, UserRole.Customer, secondId, now.AddMinutes(1));

        // Act
        var won = await firstRepository.TryConfirmResolutionAsync(first, firstId);
        var lost = await secondRepository.TryConfirmResolutionAsync(second, secondId);

        // Assert
        won.Should().BeTrue();
        lost.Should().BeFalse();
        await using var verification = new ServiceDeskDbContext(options);
        (await verification.TicketHistories.SingleAsync(h => h.Action == TicketHistoryAction.Closed)).Id.Should().Be(firstId);
        (await verification.Tickets.SingleAsync()).ClosedAt.Should().Be(now);
        (await verification.OutboxMessages.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task TryConfirmResolutionAsync_WhenHistoryInsertFails_RollsBackClosure()
    {
        // Arrange
        var ticket = await SeedAsync();
        var previousTime = ticket.UpdatedAt;
        await using var context = new ServiceDeskDbContext(options);
        var repository = new TicketRepository(context);
        var historyId = Guid.NewGuid();
        ConfirmResolutionCore.Execute(ticket, ticket.CustomerUserId, UserRole.Customer, historyId, previousTime.AddHours(1));
        await context.Database.ExecuteSqlRawAsync("CREATE TRIGGER reject_closure BEFORE INSERT ON TicketHistories WHEN NEW.Action = 'Closed' BEGIN SELECT RAISE(ABORT, 'test failure'); END;");

        // Act
        Func<Task> act = () => repository.TryConfirmResolutionAsync(ticket, historyId);

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();
        await using var verification = new ServiceDeskDbContext(options);
        var stored = await verification.Tickets.SingleAsync();
        stored.Status.Should().Be(TicketStatus.Resolved);
        stored.ClosedAt.Should().BeNull();
        stored.UpdatedAt.Should().Be(previousTime);
        (await verification.TicketHistories.CountAsync()).Should().Be(4);
        (await verification.OutboxMessages.CountAsync()).Should().Be(0);
    }

    private async Task<Ticket> SeedAsync()
    {
        var now = DateTimeOffset.UtcNow;
        var customer = new User(Guid.NewGuid(), "Ada", "Customer", "CUSTOMER@EXAMPLE.COM", UserRole.Customer, true, now, now);
        var employee = new User(Guid.NewGuid(), "Ada", "Employee", "EMPLOYEE@EXAMPLE.COM", UserRole.Employee, true, now, now);
        var ticket = CreateTicketCore.Execute(new CreateTicketCommand("VPN", "Cannot connect", TicketPriority.High),
            new CreateTicketFacts(true), customer.Id, Guid.NewGuid(), Guid.NewGuid(), now);
        AssignTicketCore.Execute(ticket, employee.Id, Guid.NewGuid(), now);
        StartWorkCore.Execute(ticket, employee.Id, Guid.NewGuid(), now);
        ResolveTicketCore.Execute(ticket, employee.Id, UserRole.Employee, Guid.NewGuid(), now);
        await using var context = new ServiceDeskDbContext(options);
        context.Users.AddRange(customer, employee);
        context.Tickets.Add(ticket);
        await context.SaveChangesAsync();
        return ticket;
    }
}
