using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Core.Tickets;
using ServiceDesk.Core.Users;
using ServiceDesk.Repository;
using ServiceDesk.Repository.Tickets;

namespace ServiceDesk.Repository.IntegrationTests.Tickets;

public sealed class TicketRepositoryTests : IAsyncLifetime
{
    private static readonly Guid CustomerUserId = Guid.Parse("0be030ef-8b48-4b49-91fe-2f4d49e070de");
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
    public async Task AddAsync_PersistsTicketAndInitialHistory()
    {
        // Arrange
        var customer = CreateCustomer();
        var ticket = CreateTicket(customer.Id);
        await using (var customerContext = new ServiceDeskDbContext(options))
        {
            customerContext.Users.Add(customer);
            await customerContext.SaveChangesAsync();
        }

        await using var context = new ServiceDeskDbContext(options);
        var repository = new TicketRepository(context);

        // Act
        await repository.AddAsync(ticket);

        // Assert
        await using var verificationContext = new ServiceDeskDbContext(options);
        var stored = await verificationContext.Tickets
            .Include(storedTicket => storedTicket.History)
            .SingleAsync();
        stored.Id.Should().Be(ticket.Id);
        stored.CustomerUserId.Should().Be(customer.Id);
        stored.History.Should().ContainSingle();
        var history = stored.History.Single();
        history.TicketId.Should().Be(stored.Id);
        history.ActorUserId.Should().Be(customer.Id);
        history.Action.Should().Be(TicketHistoryAction.Created);
        history.OccurredAt.Should().Be(stored.CreatedAt);
    }

    [Fact]
    public async Task AddAsync_WhenCustomerUserDoesNotExist_EnforcesForeignKeyIntegrity()
    {
        // Arrange
        var ticket = CreateTicket(CustomerUserId);
        await using var context = new ServiceDeskDbContext(options);
        var repository = new TicketRepository(context);

        // Act
        Func<Task> act = () => repository.AddAsync(ticket);

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task TryAssignAsync_WithOpenUnassignedTicket_PersistsAssignmentAndHistory()
    {
        // Arrange
        var customer = CreateCustomer();
        var employee = CreateEmployee();
        var ticket = CreateTicket(customer.Id, Guid.NewGuid());
        await SeedAsync(customer, employee);
        await AddTicketAsync(ticket);
        var assignmentHistoryId = Guid.NewGuid();
        await using var context = new ServiceDeskDbContext(options);
        var repository = new TicketRepository(context);
        var loadedTicket = await repository.GetByIdAsync(ticket.Id);
        AssignTicketCore.Execute(loadedTicket, employee.Id, assignmentHistoryId, DateTimeOffset.UtcNow);

        // Act
        var assigned = await repository.TryAssignAsync(loadedTicket!, assignmentHistoryId);

        // Assert
        assigned.Should().BeTrue();
        await using var verificationContext = new ServiceDeskDbContext(options);
        var stored = await verificationContext.Tickets.Include(item => item.History).SingleAsync(item => item.Id == ticket.Id);
        stored.AssignedEmployeeUserId.Should().Be(employee.Id);
        stored.History.Should().HaveCount(2);
        var assignment = stored.History.Single(history => history.Id == assignmentHistoryId);
        assignment.Action.Should().Be(TicketHistoryAction.Assigned);
        assignment.ActorUserId.Should().Be(employee.Id);
        assignment.AssignedEmployeeUserId.Should().Be(employee.Id);
    }

    [Fact]
    public async Task TryAssignAsync_WithTwoStaleUnassignedTickets_AllowsOnlyOneClaimAndHistory()
    {
        // Arrange
        var customer = CreateCustomer();
        var firstEmployee = CreateEmployee();
        var secondEmployee = CreateEmployee();
        var ticket = CreateTicket(customer.Id, Guid.NewGuid());
        await SeedAsync(customer, firstEmployee, secondEmployee);
        await AddTicketAsync(ticket);
        await using var firstContext = new ServiceDeskDbContext(options);
        await using var secondContext = new ServiceDeskDbContext(options);
        var firstRepository = new TicketRepository(firstContext);
        var secondRepository = new TicketRepository(secondContext);
        var firstTicket = await firstRepository.GetByIdAsync(ticket.Id);
        var secondTicket = await secondRepository.GetByIdAsync(ticket.Id);
        var firstHistoryId = Guid.NewGuid();
        var secondHistoryId = Guid.NewGuid();
        AssignTicketCore.Execute(firstTicket, firstEmployee.Id, firstHistoryId, DateTimeOffset.UtcNow);
        AssignTicketCore.Execute(secondTicket, secondEmployee.Id, secondHistoryId, DateTimeOffset.UtcNow);

        // Act
        var firstAssigned = await firstRepository.TryAssignAsync(firstTicket!, firstHistoryId);
        var secondAssigned = await secondRepository.TryAssignAsync(secondTicket!, secondHistoryId);

        // Assert
        firstAssigned.Should().BeTrue();
        secondAssigned.Should().BeFalse();
        await using var verificationContext = new ServiceDeskDbContext(options);
        var stored = await verificationContext.Tickets.Include(item => item.History).SingleAsync(item => item.Id == ticket.Id);
        stored.AssignedEmployeeUserId.Should().Be(firstEmployee.Id);
        stored.History.Should().HaveCount(2);
        stored.History.Should().Contain(history => history.Id == firstHistoryId);
        stored.History.Should().NotContain(history => history.Id == secondHistoryId);
    }

    [Fact]
    public async Task TryAssignAsync_WithNonOpenTicket_DoesNotPersistAssignmentOrHistory()
    {
        // Arrange
        var customer = CreateCustomer();
        var employee = CreateEmployee();
        var ticket = CreateTicket(customer.Id, Guid.NewGuid());
        await SeedAsync(customer, employee);
        await AddTicketAsync(ticket);
        await using (var statusContext = new ServiceDeskDbContext(options))
        {
            await statusContext.Database.ExecuteSqlAsync($"UPDATE Tickets SET Status = {"InProgress"} WHERE Id = {ticket.Id}");
        }

        await using var context = new ServiceDeskDbContext(options);
        var repository = new TicketRepository(context);
        var loadedTicket = await repository.GetByIdAsync(ticket.Id);

        // Act
        Action act = () => AssignTicketCore.Execute(loadedTicket, employee.Id, Guid.NewGuid(), DateTimeOffset.UtcNow);

        // Assert
        act.Should().Throw<AssignTicketException>().Which.Failure.Should().Be(AssignTicketFailureKind.TicketNotOpen);
        await using var verificationContext = new ServiceDeskDbContext(options);
        var stored = await verificationContext.Tickets.Include(item => item.History).SingleAsync(item => item.Id == ticket.Id);
        stored.AssignedEmployeeUserId.Should().BeNull();
        stored.History.Should().ContainSingle();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsTicketsAcrossStatusesPrioritiesCustomersAndAssignmentStates()
    {
        // Arrange
        var firstCustomer = CreateCustomer();
        var secondCustomer = CreateCustomer();
        var employee = CreateEmployee();
        var openTicket = CreateTicket(firstCustomer.Id, Guid.NewGuid(), TicketPriority.Low);
        var assignedTicket = CreateTicket(secondCustomer.Id, Guid.NewGuid(), TicketPriority.Critical);
        var inProgressTicket = CreateTicket(firstCustomer.Id, Guid.NewGuid(), TicketPriority.Medium);
        await SeedAsync(firstCustomer, secondCustomer, employee);
        await AddTicketAsync(openTicket);
        await AddTicketAsync(assignedTicket);
        await AddTicketAsync(inProgressTicket);
        await using (var assignmentContext = new ServiceDeskDbContext(options))
        {
            var assignmentRepository = new TicketRepository(assignmentContext);
            var loadedTicket = await assignmentRepository.GetByIdAsync(assignedTicket.Id);
            var assignmentHistoryId = Guid.NewGuid();
            AssignTicketCore.Execute(loadedTicket, employee.Id, assignmentHistoryId, DateTimeOffset.UtcNow);
            await assignmentRepository.TryAssignAsync(loadedTicket!, assignmentHistoryId);
        }

        await using (var statusContext = new ServiceDeskDbContext(options))
        {
            await statusContext.Database.ExecuteSqlAsync($"UPDATE Tickets SET Status = {"InProgress"} WHERE Id = {inProgressTicket.Id}");
        }

        await using var context = new ServiceDeskDbContext(options);
        var repository = new TicketRepository(context);

        // Act
        var tickets = await repository.GetAllAsync();

        // Assert
        tickets.Should().HaveCount(3);
        tickets.Should().Contain(ticket => ticket.Id == openTicket.Id
            && ticket.CustomerEmail == firstCustomer.Email
            && ticket.AssignedEmployeeEmail == null
            && ticket.Priority == TicketPriority.Low
            && ticket.Status == TicketStatus.Open);
        tickets.Should().Contain(ticket => ticket.Id == assignedTicket.Id
            && ticket.CustomerEmail == secondCustomer.Email
            && ticket.AssignedEmployeeEmail == employee.Email
            && ticket.Priority == TicketPriority.Critical
            && ticket.Status == TicketStatus.Open);
        tickets.Should().Contain(ticket => ticket.Id == inProgressTicket.Id
            && ticket.CustomerEmail == firstCustomer.Email
            && ticket.AssignedEmployeeEmail == null
            && ticket.Priority == TicketPriority.Medium
            && ticket.Status == TicketStatus.InProgress);
    }

    private static User CreateCustomer() => new(
        Guid.NewGuid(),
        "Ada",
        "Lovelace",
        $"ADA-{Guid.NewGuid():N}@EXAMPLE.COM",
        UserRole.Customer,
        true,
        new DateTimeOffset(2026, 9, 22, 12, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 9, 22, 12, 0, 0, TimeSpan.Zero));

    private static User CreateEmployee() => new(
        Guid.NewGuid(),
        "Grace",
        "Hopper",
        $"GRACE-{Guid.NewGuid():N}@EXAMPLE.COM",
        UserRole.Employee,
        true,
        new DateTimeOffset(2026, 9, 22, 12, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 9, 22, 12, 0, 0, TimeSpan.Zero));

    private async Task SeedAsync(params User[] users)
    {
        await using var context = new ServiceDeskDbContext(options);
        context.Users.AddRange(users);
        await context.SaveChangesAsync();
    }

    private async Task AddTicketAsync(Ticket ticket)
    {
        await using var context = new ServiceDeskDbContext(options);
        await new TicketRepository(context).AddAsync(ticket);
    }

    private static Ticket CreateTicket(
        Guid customerUserId,
        Guid? ticketId = null,
        TicketPriority priority = TicketPriority.High) =>
        CreateTicketCore.Execute(
            new CreateTicketCommand("Cannot access VPN", "The VPN rejects my credentials.", priority),
            new CreateTicketFacts(true),
            customerUserId,
            ticketId ?? Guid.Parse("247da287-7f60-4111-bbbf-cde1157402ef"),
            Guid.NewGuid(),
            new DateTimeOffset(2026, 9, 22, 12, 0, 0, TimeSpan.Zero));
}
