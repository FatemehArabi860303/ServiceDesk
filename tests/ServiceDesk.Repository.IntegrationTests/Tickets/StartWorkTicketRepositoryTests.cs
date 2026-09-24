using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Core.Tickets;
using ServiceDesk.Core.Users;
using ServiceDesk.Repository;
using ServiceDesk.Repository.Tickets;

namespace ServiceDesk.Repository.IntegrationTests.Tickets;

public sealed class StartWorkTicketRepositoryTests : IAsyncLifetime
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
    public async Task TryStartWorkAsync_WithOpenTicketAssignedToEmployee_PersistsStatusAndHistory()
    {
        // Arrange
        var customer = CreateUser(UserRole.Customer);
        var employee = CreateUser(UserRole.Employee);
        var ticket = CreateTicket(customer.Id);
        await SeedAsync(customer, employee, ticket);
        await AssignAsync(ticket.Id, employee.Id);
        await using var context = new ServiceDeskDbContext(options);
        var repository = new TicketRepository(context);
        var loadedTicket = await repository.GetByIdAsync(ticket.Id);
        var historyId = Guid.NewGuid();
        var startTime = DateTimeOffset.UtcNow;
        StartWorkCore.Execute(loadedTicket, employee.Id, historyId, startTime);

        // Act
        var started = await repository.TryStartWorkAsync(loadedTicket!, historyId);

        // Assert
        started.Should().BeTrue();
        await using var verificationContext = new ServiceDeskDbContext(options);
        var storedTicket = await verificationContext.Tickets.Include(item => item.History).SingleAsync(item => item.Id == ticket.Id);
        storedTicket.Status.Should().Be(TicketStatus.InProgress);
        storedTicket.AssignedEmployeeUserId.Should().Be(employee.Id);
        storedTicket.CreatedAt.Should().Be(ticket.CreatedAt);
        storedTicket.UpdatedAt.Should().Be(startTime);
        var history = storedTicket.History.Single(item => item.Id == historyId);
        history.Action.Should().Be(TicketHistoryAction.WorkStarted);
        history.ActorUserId.Should().Be(employee.Id);
        history.AssignedEmployeeUserId.Should().BeNull();
    }

    [Fact]
    public async Task TryStartWorkAsync_WithTwoStaleAssignedTickets_AllowsOnlyOneStartAndHistory()
    {
        // Arrange
        var customer = CreateUser(UserRole.Customer);
        var employee = CreateUser(UserRole.Employee);
        var ticket = CreateTicket(customer.Id);
        await SeedAsync(customer, employee, ticket);
        await AssignAsync(ticket.Id, employee.Id);
        await using var firstContext = new ServiceDeskDbContext(options);
        await using var secondContext = new ServiceDeskDbContext(options);
        var firstRepository = new TicketRepository(firstContext);
        var secondRepository = new TicketRepository(secondContext);
        var firstTicket = await firstRepository.GetByIdAsync(ticket.Id);
        var secondTicket = await secondRepository.GetByIdAsync(ticket.Id);
        var firstHistoryId = Guid.NewGuid();
        var secondHistoryId = Guid.NewGuid();
        StartWorkCore.Execute(firstTicket, employee.Id, firstHistoryId, DateTimeOffset.UtcNow);
        StartWorkCore.Execute(secondTicket, employee.Id, secondHistoryId, DateTimeOffset.UtcNow);

        // Act
        var firstStarted = await firstRepository.TryStartWorkAsync(firstTicket!, firstHistoryId);
        var secondStarted = await secondRepository.TryStartWorkAsync(secondTicket!, secondHistoryId);

        // Assert
        firstStarted.Should().BeTrue();
        secondStarted.Should().BeFalse();
        await using var verificationContext = new ServiceDeskDbContext(options);
        var storedTicket = await verificationContext.Tickets.Include(item => item.History).SingleAsync(item => item.Id == ticket.Id);
        storedTicket.Status.Should().Be(TicketStatus.InProgress);
        storedTicket.History.Should().Contain(history => history.Id == firstHistoryId);
        storedTicket.History.Should().NotContain(history => history.Id == secondHistoryId);
    }

    private async Task AssignAsync(Guid ticketId, Guid employeeUserId)
    {
        await using var context = new ServiceDeskDbContext(options);
        var repository = new TicketRepository(context);
        var ticket = await repository.GetByIdAsync(ticketId);
        var historyId = Guid.NewGuid();
        AssignTicketCore.Execute(ticket, employeeUserId, historyId, DateTimeOffset.UtcNow);
        await repository.TryAssignAsync(ticket!, historyId);
    }

    private async Task SeedAsync(params object[] values)
    {
        await using var context = new ServiceDeskDbContext(options);
        context.Users.AddRange(values.OfType<User>());
        context.Tickets.AddRange(values.OfType<Ticket>());
        await context.SaveChangesAsync();
    }

    private static Ticket CreateTicket(Guid customerUserId) => CreateTicketCore.Execute(
        new CreateTicketCommand("Cannot access VPN", "The VPN rejects my credentials.", TicketPriority.High),
        new CreateTicketFacts(true),
        customerUserId,
        Guid.NewGuid(),
        Guid.NewGuid(),
        DateTimeOffset.UtcNow);

    private static User CreateUser(UserRole role) => new(
        Guid.NewGuid(),
        "Ada",
        "Lovelace",
        $"ADA-{Guid.NewGuid():N}@EXAMPLE.COM",
        role,
        true,
        DateTimeOffset.UtcNow,
        DateTimeOffset.UtcNow);
}
