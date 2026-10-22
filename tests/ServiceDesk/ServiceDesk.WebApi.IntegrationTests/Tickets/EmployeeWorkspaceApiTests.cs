using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceDesk.Core.Tickets;
using ServiceDesk.Core.Users;
using ServiceDesk.Repository;
using ServiceDesk.Repository.Tickets;
using ServiceDesk.Shell.Notifications;
using ServiceDesk.Shell.Tickets;
using ServiceDesk.WebApi.Authentication;
using ServiceDesk.WebApi.IntegrationTests.Users;

namespace ServiceDesk.WebApi.IntegrationTests.Tickets;

public sealed class EmployeeWorkspaceApiTests : IDisposable
{
    private readonly ServiceDeskApiFactory factory = new();
    private readonly HttpClient client;
    public EmployeeWorkspaceApiTests() => client = factory.CreateClient();
    public void Dispose() { client.Dispose(); factory.Dispose(); }

    [Fact]
    public async Task AnonymousWorkspace_ReturnsUnauthorized()
    {
        // Arrange
        Guid id = Guid.NewGuid();
        // Act
        HttpResponseMessage page = await client.GetAsync("/api/employee/tickets");
        HttpResponseMessage details = await client.GetAsync($"/api/employee/tickets/{id}");
        HttpResponseMessage start = await client.PostAsync($"/api/employee/tickets/{id}/start-work", null);
        // Assert
        page.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        details.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        start.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.Customer, true)]
    [InlineData(UserRole.Administrator, true)]
    [InlineData(UserRole.Employee, false)]
    public async Task Workspace_RejectsWrongRoleOrInactiveEmployee(UserRole role, bool active)
    {
        // Arrange
        User user = CreateUser(role, active);
        await SeedAsync(user);
        SetToken(user);
        Guid id = Guid.NewGuid();
        // Act
        HttpResponseMessage page = await client.GetAsync("/api/employee/tickets");
        HttpResponseMessage details = await client.GetAsync($"/api/employee/tickets/{id}");
        HttpResponseMessage start = await client.PostAsync($"/api/employee/tickets/{id}/start-work", null);
        // Assert
        page.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        details.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        start.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetPages_AcrossCustomers_IsBoundedAndDetailsIncludeHistory()
    {
        // Arrange
        User employee = CreateUser(UserRole.Employee);
        User firstCustomer = CreateUser(UserRole.Customer);
        User secondCustomer = CreateUser(UserRole.Customer);
        Ticket first = CreateTicket(firstCustomer.Id);
        Ticket second = CreateTicket(secondCustomer.Id);
        await SeedAsync(employee, firstCustomer, secondCustomer, first, second);
        SetToken(employee);
        // Act
        EmployeeTicketPage? page1 = await client.GetFromJsonAsync<EmployeeTicketPage>("/api/employee/tickets?page=1&pageSize=1");
        EmployeeTicketPage? page2 = await client.GetFromJsonAsync<EmployeeTicketPage>("/api/employee/tickets?page=2&pageSize=1");
        EmployeeTicketDetails? details = await client.GetFromJsonAsync<EmployeeTicketDetails>($"/api/employee/tickets/{first.Id}");
        // Assert
        page1!.Items.Should().HaveCount(1);
        page1.HasMore.Should().BeTrue();
        page2!.Items.Should().HaveCount(1);
        page2.HasMore.Should().BeFalse();
        page1.Items.Concat(page2.Items).Select(item => item.CustomerEmail).Should().BeEquivalentTo(firstCustomer.Email, secondCustomer.Email);
        details!.Description.Should().Be(first.Description);
        details.CanStartWork.Should().BeTrue();
        details.History.Should().ContainSingle(item => item.Action == TicketHistoryAction.Created);
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=51")]
    [InlineData("pageSize=0")]
    public async Task InvalidPagination_ReturnsBadRequest(string query)
    {
        // Arrange
        User employee = CreateUser(UserRole.Employee);
        await SeedAsync(employee); SetToken(employee);
        // Act
        HttpResponseMessage response = await client.GetAsync($"/api/employee/tickets?{query}");
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StartWork_AtomicallyClaimsOrStartsOwnAssignment_AndIgnoresForgedIdentity(bool assigned)
    {
        // Arrange
        User employee = CreateUser(UserRole.Employee);
        User customer = CreateUser(UserRole.Customer);
        Ticket ticket = CreateTicket(customer.Id);
        if (assigned) AssignTicketCore.Execute(ticket, employee.Id, Guid.NewGuid(), DateTimeOffset.UtcNow);
        await SeedAsync(employee, customer, ticket); SetToken(employee);
        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync($"/api/employee/tickets/{ticket.Id}/start-work", new { employeeUserId = Guid.NewGuid() });
        EmployeeTicketDetails? details = await client.GetFromJsonAsync<EmployeeTicketDetails>($"/api/employee/tickets/{ticket.Id}");
        HttpResponseMessage repeated = await client.PostAsync($"/api/employee/tickets/{ticket.Id}/start-work", null);
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        details!.Ticket.Status.Should().Be(TicketStatus.InProgress);
        details.Ticket.AssignedEmployeeUserId.Should().Be(employee.Id);
        details.Ticket.AssignedEmployeeEmail.Should().Be(employee.Email);
        details.CanStartWork.Should().BeFalse();
        details.History.Select(item => item.Action).Should().Equal(TicketHistoryAction.Created, TicketHistoryAction.Assigned, TicketHistoryAction.WorkStarted);
        details.History.Where(item => item.Action != TicketHistoryAction.Created).Should().OnlyContain(item => item.ActorUserId == employee.Id);
        repeated.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using IServiceScope scope = factory.Services.CreateScope();
        ServiceDeskDbContext context = scope.ServiceProvider.GetRequiredService<ServiceDeskDbContext>();
        (await context.OutboxMessages.CountAsync()).Should().Be(assigned ? 1 : 2);
    }

    [Fact]
    public async Task AnotherEmployeeCannotOverwriteAssignment()
    {
        // Arrange
        User employee = CreateUser(UserRole.Employee);
        User owner = CreateUser(UserRole.Employee);
        User customer = CreateUser(UserRole.Customer);
        Ticket ticket = CreateTicket(customer.Id);
        AssignTicketCore.Execute(ticket, owner.Id, Guid.NewGuid(), DateTimeOffset.UtcNow);
        await SeedAsync(employee, owner, customer, ticket); SetToken(employee);
        // Act
        HttpResponseMessage response = await client.PostAsync($"/api/employee/tickets/{ticket.Id}/start-work", null);
        EmployeeTicketDetails? details = await client.GetFromJsonAsync<EmployeeTicketDetails>($"/api/employee/tickets/{ticket.Id}");
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        details!.Ticket.AssignedEmployeeUserId.Should().Be(owner.Id);
        details.Ticket.Status.Should().Be(TicketStatus.Open);
        details.CanStartWork.Should().BeFalse();
        details.History.Should().HaveCount(2);
    }

    [Fact]
    public async Task MissingTicket_ReturnsNotFoundForReadAndStart()
    {
        // Arrange
        User employee = CreateUser(UserRole.Employee);
        await SeedAsync(employee); SetToken(employee);
        Guid missingId = Guid.NewGuid();
        // Act
        HttpResponseMessage details = await client.GetAsync($"/api/employee/tickets/{missingId}");
        HttpResponseMessage start = await client.PostAsync($"/api/employee/tickets/{missingId}/start-work", null);
        // Assert
        details.StatusCode.Should().Be(HttpStatusCode.NotFound);
        start.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task StaleEmployeeRoleClaim_DoesNotOverridePersistedRole()
    {
        // Arrange
        User customer = CreateUser(UserRole.Customer);
        await SeedAsync(customer);
        SetToken(customer with { Role = UserRole.Employee });
        // Act
        HttpResponseMessage list = await client.GetAsync("/api/employee/tickets");
        HttpResponseMessage start = await client.PostAsync($"/api/employee/tickets/{Guid.NewGuid()}/start-work", null);
        // Assert
        list.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        start.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData(TicketStatus.InProgress)]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    public async Task UnsupportedLifecycleState_IsNotRestarted(TicketStatus status)
    {
        // Arrange
        User employee = CreateUser(UserRole.Employee);
        User customer = CreateUser(UserRole.Customer);
        Ticket ticket = CreateTicket(customer.Id);
        AssignTicketCore.Execute(ticket, employee.Id, Guid.NewGuid(), DateTimeOffset.UtcNow);
        StartWorkCore.Execute(ticket, employee.Id, Guid.NewGuid(), DateTimeOffset.UtcNow);
        if (status is TicketStatus.Resolved or TicketStatus.Closed)
            ResolveTicketCore.Execute(ticket, employee.Id, UserRole.Employee, Guid.NewGuid(), DateTimeOffset.UtcNow);
        if (status == TicketStatus.Closed)
            ConfirmResolutionCore.Execute(ticket, customer.Id, UserRole.Customer, Guid.NewGuid(), DateTimeOffset.UtcNow);
        int expectedHistoryCount = ticket.History.Count;
        await SeedAsync(employee, customer, ticket); SetToken(employee);
        // Act
        HttpResponseMessage response = await client.PostAsync($"/api/employee/tickets/{ticket.Id}/start-work", null);
        EmployeeTicketDetails? details = await client.GetFromJsonAsync<EmployeeTicketDetails>($"/api/employee/tickets/{ticket.Id}");
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        details!.Ticket.Status.Should().Be(status);
        details.CanStartWork.Should().BeFalse();
        details.History.Should().HaveCount(expectedHistoryCount);
    }

    [Fact]
    public async Task ConditionalRepositoryWrite_RejectsStaleClaimWithoutExtraHistoryOrOutbox()
    {
        // Arrange
        User employee = CreateUser(UserRole.Employee);
        User other = CreateUser(UserRole.Employee);
        User customer = CreateUser(UserRole.Customer);
        Ticket ticket = CreateTicket(customer.Id);
        await SeedAsync(employee, other, customer, ticket);
        using IServiceScope firstScope = factory.Services.CreateScope();
        using IServiceScope secondScope = factory.Services.CreateScope();
        TicketRepository firstRepository = new(firstScope.ServiceProvider.GetRequiredService<ServiceDeskDbContext>());
        TicketRepository secondRepository = new(secondScope.ServiceProvider.GetRequiredService<ServiceDeskDbContext>());
        Ticket first = (await firstRepository.GetByIdAsync(ticket.Id))!;
        Ticket second = (await secondRepository.GetByIdAsync(ticket.Id))!;
        Guid firstAssignment = Guid.NewGuid(); Guid firstStarted = Guid.NewGuid();
        Guid secondAssignment = Guid.NewGuid(); Guid secondStarted = Guid.NewGuid();
        IReadOnlyList<RequestProgressed> firstEvents = EmployeeWorkCore.Execute(first, employee.Id, firstAssignment, firstStarted, DateTimeOffset.UtcNow);
        IReadOnlyList<RequestProgressed> secondEvents = EmployeeWorkCore.Execute(second, other.Id, secondAssignment, secondStarted, DateTimeOffset.UtcNow);
        // Act
        bool winner = await firstRepository.TryClaimAndStartAsync(first, null, [firstAssignment, firstStarted], Notifications(firstEvents), default);
        bool loser = await secondRepository.TryClaimAndStartAsync(second, null, [secondAssignment, secondStarted], Notifications(secondEvents), default);
        // Assert
        winner.Should().BeTrue(); loser.Should().BeFalse();
        using IServiceScope verificationScope = factory.Services.CreateScope();
        ServiceDeskDbContext context = verificationScope.ServiceProvider.GetRequiredService<ServiceDeskDbContext>();
        Ticket saved = await context.Tickets.Include(item => item.History).SingleAsync();
        saved.AssignedEmployeeUserId.Should().Be(employee.Id);
        saved.History.Should().HaveCount(3);
        (await context.OutboxMessages.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task FailedOutboxInsert_RollsBackAssignmentStatusAndHistory()
    {
        // Arrange
        User employee = CreateUser(UserRole.Employee);
        User customer = CreateUser(UserRole.Customer);
        Ticket ticket = CreateTicket(customer.Id);
        await SeedAsync(employee, customer, ticket);
        using IServiceScope scope = factory.Services.CreateScope();
        ServiceDeskDbContext context = scope.ServiceProvider.GetRequiredService<ServiceDeskDbContext>();
        await context.Database.ExecuteSqlRawAsync("CREATE TRIGGER reject_workspace_outbox BEFORE INSERT ON OutboxMessages BEGIN SELECT RAISE(ABORT, 'synthetic rollback'); END;");
        TicketRepository repository = new(context);
        Ticket loaded = (await repository.GetByIdAsync(ticket.Id))!;
        Guid assignedId = Guid.NewGuid(); Guid startedId = Guid.NewGuid();
        IReadOnlyList<RequestProgressed> events = EmployeeWorkCore.Execute(loaded, employee.Id, assignedId, startedId, DateTimeOffset.UtcNow);
        // Act
        Func<Task> act = async () => await repository.TryClaimAndStartAsync(loaded, null, [assignedId, startedId], Notifications(events), default);
        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();
        using IServiceScope verificationScope = factory.Services.CreateScope();
        ServiceDeskDbContext verify = verificationScope.ServiceProvider.GetRequiredService<ServiceDeskDbContext>();
        Ticket saved = await verify.Tickets.Include(item => item.History).SingleAsync();
        saved.Status.Should().Be(TicketStatus.Open);
        saved.AssignedEmployeeUserId.Should().BeNull();
        saved.History.Should().ContainSingle();
        (await verify.OutboxMessages.CountAsync()).Should().Be(0);
    }

    private async Task SeedAsync(params object[] values)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        ServiceDeskDbContext context = scope.ServiceProvider.GetRequiredService<ServiceDeskDbContext>();
        context.Users.AddRange(values.OfType<User>());
        context.Tickets.AddRange(values.OfType<Ticket>());
        await context.SaveChangesAsync();
    }
    private void SetToken(User user)
    {
        JwtAccessTokenIssuer issuer = new(new JwtOptions { Issuer = ServiceDeskApiFactory.JwtIssuer,
            Audience = ServiceDeskApiFactory.JwtAudience, SigningKey = ServiceDeskApiFactory.JwtSigningKey });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", issuer.Issue(user, DateTimeOffset.UtcNow).Value);
    }
    private static TicketProgressNotification[] Notifications(IReadOnlyList<RequestProgressed> events) =>
        events.Select(item => new TicketProgressNotification(Guid.NewGuid(), item, item.OccurredAt)).ToArray();
    private static Ticket CreateTicket(Guid customerId) => CreateTicketCore.Execute(
        new CreateTicketCommand("Employee test request", "Synthetic ticket details", TicketPriority.Medium),
        new CreateTicketFacts(true), customerId, Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);
    private static User CreateUser(UserRole role, bool active = true) => new(Guid.NewGuid(), "Test", "User",
        $"TEST-{Guid.NewGuid():N}@EXAMPLE.INVALID", role, active, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
}
