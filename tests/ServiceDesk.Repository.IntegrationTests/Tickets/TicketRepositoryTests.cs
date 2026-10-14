using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Notification.Contracts;
using ServiceDesk.Core.Tickets;
using ServiceDesk.Core.Users;
using ServiceDesk.Repository;
using ServiceDesk.Repository.Notifications;
using ServiceDesk.Repository.Tickets;
using ServiceDesk.Shell.Notifications;
using ServiceDesk.Shell.Tickets;
using System.Text.Json;

namespace ServiceDesk.Repository.IntegrationTests.Tickets;

public sealed class TicketRepositoryTests : IAsyncLifetime
{
    private static readonly Guid CustomerUserId =
        Guid.Parse("0be030ef-8b48-4b49-91fe-2f4d49e070de");

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
    public async Task AddAsync_PersistsTicketAndInitialHistory()
    {
        // Arrange
        User customer = CreateCustomer();
        Ticket ticket = CreateTicket(customer.Id);
        await SeedAsync(customer);

        await using ServiceDeskDbContext context = new(options);
        TicketRepository repository = new(context);

        // Act
        await repository.AddAsync(ticket);

        await using ServiceDeskDbContext verification = new(options);
        Ticket stored = await verification.Tickets
            .Include(item => item.History)
            .SingleAsync();

        TicketHistory history = stored.History.Single();

        // Assert
        stored.Id.Should().Be(ticket.Id);
        stored.CustomerUserId.Should().Be(customer.Id);
        stored.History.Should().ContainSingle();
        history.TicketId.Should().Be(stored.Id);
        history.ActorUserId.Should().Be(customer.Id);
        history.Action.Should().Be(TicketHistoryAction.Created);
        history.OccurredAt.Should().Be(stored.CreatedAt);
    }

    [Fact]
    public async Task AddAsync_WhenCustomerUserDoesNotExist_EnforcesForeignKeyIntegrity()
    {
        // Arrange
        Ticket ticket = CreateTicket(CustomerUserId);

        await using ServiceDeskDbContext context = new(options);
        TicketRepository repository = new(context);
        Func<Task> operation = () => repository.AddAsync(ticket);

        // Act
        Exception? exception = await Record.ExceptionAsync(operation);

        // Assert
        exception.Should().BeOfType<DbUpdateException>();
    }

    [Fact]
    public async Task TryAssignAsync_WithOpenUnassignedTicket_PersistsAssignmentAndHistory()
    {
        // Arrange
        User customer = CreateCustomer();
        User employee = CreateEmployee();
        Ticket ticket = CreateTicket(customer.Id, Guid.NewGuid());

        await SeedAsync(customer, employee);
        await AddTicketAsync(ticket);

        Guid historyId = Guid.NewGuid();

        await using ServiceDeskDbContext context = new(options);
        TicketRepository repository = new(context);
        Ticket loadedTicket = (await repository.GetByIdAsync(ticket.Id))!;

        RequestProgressed progressed = AssignTicketCore.Execute(
            loadedTicket,
            employee.Id,
            historyId,
            DateTimeOffset.UtcNow);

        TicketProgressNotification notification = CreateNotification(progressed);

        NotificationRequestedV1 expectedNotification = new(
            "servicedesk",
            notification.EventId.ToString(),
            customer.Email,
            "Your service request has been assigned",
            "Your service request has been assigned to a support employee.");

        // Act
        bool assigned = await repository.TryAssignAsync(
            loadedTicket,
            historyId,
            notification);

        await using ServiceDeskDbContext verification = new(options);
        Ticket stored = await verification.Tickets
            .Include(item => item.History)
            .SingleAsync(item => item.Id == ticket.Id);

        TicketHistory assignment = stored.History.Single(
            item => item.Id == historyId);

        OutboxMessage outbox = await verification.OutboxMessages
            .SingleAsync(item => item.Id == notification.EventId);

        NotificationRequestedV1? actualNotification =
            JsonSerializer.Deserialize<NotificationRequestedV1>(outbox.Payload);

        // Assert
        assigned.Should().BeTrue();
        stored.AssignedEmployeeUserId.Should().Be(employee.Id);
        stored.History.Should().HaveCount(2);
        assignment.Action.Should().Be(TicketHistoryAction.Assigned);
        assignment.ActorUserId.Should().Be(employee.Id);
        assignment.AssignedEmployeeUserId.Should().Be(employee.Id);
        outbox.Type.Should().Be(NotificationRequestedV1.Type);
        outbox.PublishedAt.Should().BeNull();
        actualNotification.Should().Be(expectedNotification);
    }

    [Fact]
    public async Task TryAssignAsync_WithTwoStaleUnassignedTickets_AllowsOnlyOneClaimAndHistory()
    {
        // Arrange
        User customer = CreateCustomer();
        User firstEmployee = CreateEmployee();
        User secondEmployee = CreateEmployee();
        Ticket ticket = CreateTicket(customer.Id, Guid.NewGuid());

        await SeedAsync(customer, firstEmployee, secondEmployee);
        await AddTicketAsync(ticket);

        await using ServiceDeskDbContext firstContext = new(options);
        await using ServiceDeskDbContext secondContext = new(options);

        TicketRepository firstRepository = new(firstContext);
        TicketRepository secondRepository = new(secondContext);
        Ticket firstTicket = (await firstRepository.GetByIdAsync(ticket.Id))!;
        Ticket secondTicket = (await secondRepository.GetByIdAsync(ticket.Id))!;
        Guid firstHistoryId = Guid.NewGuid();
        Guid secondHistoryId = Guid.NewGuid();

        RequestProgressed firstProgressed = AssignTicketCore.Execute(
            firstTicket,
            firstEmployee.Id,
            firstHistoryId,
            DateTimeOffset.UtcNow);

        RequestProgressed secondProgressed = AssignTicketCore.Execute(
            secondTicket,
            secondEmployee.Id,
            secondHistoryId,
            DateTimeOffset.UtcNow);

        TicketProgressNotification firstNotification =
            CreateNotification(firstProgressed);

        TicketProgressNotification secondNotification =
            CreateNotification(secondProgressed);

        // Act
        bool firstAssigned = await firstRepository.TryAssignAsync(
            firstTicket,
            firstHistoryId,
            firstNotification);

        bool secondAssigned = await secondRepository.TryAssignAsync(
            secondTicket,
            secondHistoryId,
            secondNotification);

        await using ServiceDeskDbContext verification = new(options);
        Ticket stored = await verification.Tickets
            .Include(item => item.History)
            .SingleAsync(item => item.Id == ticket.Id);

        int notificationCount = await verification.OutboxMessages
            .CountAsync(item => item.Type == NotificationRequestedV1.Type);

        bool hasLosingNotification = await verification.OutboxMessages
            .AnyAsync(item => item.Id == secondNotification.EventId);

        // Assert
        firstAssigned.Should().BeTrue();
        secondAssigned.Should().BeFalse();
        stored.AssignedEmployeeUserId.Should().Be(firstEmployee.Id);
        stored.History.Should().HaveCount(2);
        stored.History.Should().Contain(item => item.Id == firstHistoryId);
        stored.History.Should().NotContain(item => item.Id == secondHistoryId);
        notificationCount.Should().Be(1);
        hasLosingNotification.Should().BeFalse();
    }

    [Fact]
    public async Task TryAssignAsync_WithNonOpenTicket_DoesNotPersistAssignmentOrHistory()
    {
        // Arrange
        User customer = CreateCustomer();
        User employee = CreateEmployee();
        Ticket ticket = CreateTicket(customer.Id, Guid.NewGuid());

        await SeedAsync(customer, employee);
        await AddTicketAsync(ticket);

        await using (ServiceDeskDbContext statusContext = new(options))
        {
            await statusContext.Database.ExecuteSqlAsync(
                $"UPDATE Tickets SET Status = {"InProgress"} WHERE Id = {ticket.Id}");
        }

        await using ServiceDeskDbContext context = new(options);
        TicketRepository repository = new(context);
        Ticket loadedTicket = (await repository.GetByIdAsync(ticket.Id))!;
        Guid historyId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        Action operation = () => AssignTicketCore.Execute(
            loadedTicket,
            employee.Id,
            historyId,
            now);

        // Act
        Exception? exception = Record.Exception(operation);

        await using ServiceDeskDbContext verification = new(options);
        Ticket stored = await verification.Tickets
            .Include(item => item.History)
            .SingleAsync(item => item.Id == ticket.Id);

        // Assert
        exception.Should().BeOfType<AssignTicketException>()
            .Which.Failure.Should().Be(AssignTicketFailureKind.TicketNotOpen);

        stored.AssignedEmployeeUserId.Should().BeNull();
        stored.History.Should().ContainSingle();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsTicketsAcrossStatusesPrioritiesCustomersAndAssignmentStates()
    {
        // Arrange
        User firstCustomer = CreateCustomer();
        User secondCustomer = CreateCustomer();
        User employee = CreateEmployee();

        Ticket openTicket = CreateTicket(
            firstCustomer.Id, Guid.NewGuid(), TicketPriority.Low);

        Ticket assignedTicket = CreateTicket(
            secondCustomer.Id, Guid.NewGuid(), TicketPriority.Critical);

        Ticket inProgressTicket = CreateTicket(
            firstCustomer.Id, Guid.NewGuid(), TicketPriority.Medium);

        await SeedAsync(firstCustomer, secondCustomer, employee);
        await AddTicketAsync(openTicket);
        await AddTicketAsync(assignedTicket);
        await AddTicketAsync(inProgressTicket);

        await using (ServiceDeskDbContext assignmentContext = new(options))
        {
            TicketRepository assignmentRepository = new(assignmentContext);
            Ticket loadedTicket =
                (await assignmentRepository.GetByIdAsync(assignedTicket.Id))!;

            Guid historyId = Guid.NewGuid();

            RequestProgressed progressed = AssignTicketCore.Execute(
                loadedTicket,
                employee.Id,
                historyId,
                DateTimeOffset.UtcNow);

            TicketProgressNotification notification =
                CreateNotification(progressed);

            await assignmentRepository.TryAssignAsync(
                loadedTicket,
                historyId,
                notification);
        }

        await using (ServiceDeskDbContext statusContext = new(options))
        {
            await statusContext.Database.ExecuteSqlAsync(
                $"UPDATE Tickets SET Status = {"InProgress"} WHERE Id = {inProgressTicket.Id}");
        }

        await using ServiceDeskDbContext context = new(options);
        TicketRepository repository = new(context);

        // Act
        IReadOnlyList<TicketListItem> tickets = await repository.GetAllAsync();

        // Assert
        tickets.Should().HaveCount(3);

        tickets.Should().Contain(item => item.Id == openTicket.Id
            && item.CustomerEmail == firstCustomer.Email
            && item.AssignedEmployeeEmail == null
            && item.Priority == TicketPriority.Low
            && item.Status == TicketStatus.Open);

        tickets.Should().Contain(item => item.Id == assignedTicket.Id
            && item.CustomerEmail == secondCustomer.Email
            && item.AssignedEmployeeEmail == employee.Email
            && item.Priority == TicketPriority.Critical
            && item.Status == TicketStatus.Open);

        tickets.Should().Contain(item => item.Id == inProgressTicket.Id
            && item.CustomerEmail == firstCustomer.Email
            && item.AssignedEmployeeEmail == null
            && item.Priority == TicketPriority.Medium
            && item.Status == TicketStatus.InProgress);
    }

    private static User CreateCustomer() => CreateUser(UserRole.Customer);

    private static User CreateEmployee() => CreateUser(UserRole.Employee);

    private static User CreateUser(UserRole role) => new(
        Guid.NewGuid(),
        "Ada",
        "Lovelace",
        $"ADA-{Guid.NewGuid():N}@EXAMPLE.COM",
        role,
        true,
        new DateTimeOffset(2026, 9, 22, 12, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 9, 22, 12, 0, 0, TimeSpan.Zero));

    private async Task SeedAsync(params User[] users)
    {
        await using ServiceDeskDbContext context = new(options);
        context.Users.AddRange(users);
        await context.SaveChangesAsync();
    }

    private async Task AddTicketAsync(Ticket ticket)
    {
        await using ServiceDeskDbContext context = new(options);
        TicketRepository repository = new(context);
        await repository.AddAsync(ticket);
    }

    private static Ticket CreateTicket(
        Guid customerUserId,
        Guid? ticketId = null,
        TicketPriority priority = TicketPriority.High) =>
        CreateTicketCore.Execute(
            new CreateTicketCommand(
                "Cannot access VPN",
                "The VPN rejects my credentials.",
                priority),
            new CreateTicketFacts(true),
            customerUserId,
            ticketId ?? Guid.Parse("247da287-7f60-4111-bbbf-cde1157402ef"),
            Guid.NewGuid(),
            new DateTimeOffset(2026, 9, 22, 12, 0, 0, TimeSpan.Zero));

    private static TicketProgressNotification CreateNotification(
        RequestProgressed progressed) => new(
        Guid.NewGuid(),
        progressed,
        progressed.OccurredAt);
}