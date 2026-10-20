using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using ServiceDesk.Core.Tickets;
using ServiceDesk.Core.Users;
using ServiceDesk.Repository;
using ServiceDesk.WebApi.Authentication;
using ServiceDesk.WebApi.IntegrationTests.Users;

namespace ServiceDesk.WebApi.IntegrationTests.Tickets;

public sealed class StartWorkApiTests : IClassFixture<ServiceDeskApiFactory>
{
    private readonly ServiceDeskApiFactory factory;
    private readonly HttpClient client;

    public StartWorkApiTests(ServiceDeskApiFactory factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
    }

    [Fact]
    public async Task PostStartWorkWithoutJwt_ReturnsUnauthorized()
    {
        // Arrange
        var ticketId = Guid.NewGuid();

        // Act
        var response = await client.PostAsync($"/api/tickets/{ticketId}/start-work", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostStartWorkWithInvalidJwt_ReturnsUnauthorized()
    {
        // Arrange
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-valid-token");

        // Act
        var response = await client.PostAsync($"/api/tickets/{Guid.NewGuid()}/start-work", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostStartWorkWithMissingJwtSubject_ReturnsForbidden()
    {
        // Arrange
        SetEmployeeTokenWithoutSubject();

        // Act
        var response = await client.PostAsync($"/api/tickets/{Guid.NewGuid()}/start-work", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostStartWorkWithAssignedEmployee_TransitionsTicketAndRecordsJwtActor()
    {
        // Arrange
        var customer = CreateUser(UserRole.Customer);
        var employee = CreateUser(UserRole.Employee);
        var ticket = CreateAssignedTicket(customer.Id, employee.Id);
        await SeedAsync(customer, employee, ticket);
        SetBearerToken(employee);

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/tickets/{ticket.Id}/start-work",
            new { employeeUserId = Guid.NewGuid(), actorUserId = Guid.NewGuid() });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var storedTicket = await GetTicketAsync(ticket.Id);
        storedTicket.Status.Should().Be(TicketStatus.InProgress);
        storedTicket.AssignedEmployeeUserId.Should().Be(employee.Id);
        storedTicket.CreatedAt.Should().Be(ticket.CreatedAt);
        storedTicket.UpdatedAt.Should().BeAfter(ticket.UpdatedAt);
        var history = storedTicket.History.Single(item => item.Action == TicketHistoryAction.WorkStarted);
        history.ActorUserId.Should().Be(employee.Id);
        history.AssignedEmployeeUserId.Should().BeNull();
    }

    [Theory]
    [InlineData(UserRole.Customer)]
    [InlineData(UserRole.Administrator)]
    public async Task PostStartWorkWithNonEmployeeJwt_ReturnsForbidden(UserRole role)
    {
        // Arrange
        var customer = CreateUser(UserRole.Customer);
        var employee = CreateUser(UserRole.Employee);
        var caller = CreateUser(role);
        var ticket = CreateAssignedTicket(customer.Id, employee.Id);
        await SeedAsync(customer, employee, caller, ticket);
        SetBearerToken(caller);

        // Act
        var response = await client.PostAsync($"/api/tickets/{ticket.Id}/start-work", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var storedTicket = await GetTicketAsync(ticket.Id);
        storedTicket.Status.Should().Be(TicketStatus.Open);
        storedTicket.History.Should().HaveCount(2);
    }

    [Fact]
    public async Task PostStartWorkWithUnassignedTicket_ReturnsConflictWithoutAutomaticAssignment()
    {
        // Arrange
        var customer = CreateUser(UserRole.Customer);
        var employee = CreateUser(UserRole.Employee);
        var ticket = CreateTicket(customer.Id);
        await SeedAsync(customer, employee, ticket);
        SetBearerToken(employee);

        // Act
        var response = await client.PostAsync($"/api/tickets/{ticket.Id}/start-work", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var storedTicket = await GetTicketAsync(ticket.Id);
        storedTicket.Status.Should().Be(TicketStatus.Open);
        storedTicket.AssignedEmployeeUserId.Should().BeNull();
        storedTicket.History.Should().ContainSingle();
    }

    [Fact]
    public async Task PostStartWorkWithAnotherEmployee_ReturnsForbidden()
    {
        // Arrange
        var customer = CreateUser(UserRole.Customer);
        var assignedEmployee = CreateUser(UserRole.Employee);
        var otherEmployee = CreateUser(UserRole.Employee);
        var ticket = CreateAssignedTicket(customer.Id, assignedEmployee.Id);
        await SeedAsync(customer, assignedEmployee, otherEmployee, ticket);
        SetBearerToken(otherEmployee);

        // Act
        var response = await client.PostAsync($"/api/tickets/{ticket.Id}/start-work", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var storedTicket = await GetTicketAsync(ticket.Id);
        storedTicket.Status.Should().Be(TicketStatus.Open);
        storedTicket.History.Should().HaveCount(2);
    }

    [Fact]
    public async Task PostStartWorkWithEmployeeJwt_DoesNotReloadPersistedRoleOrActiveState()
    {
        // Arrange
        var customer = CreateUser(UserRole.Customer);
        var caller = CreateUser(UserRole.Customer) with { IsActive = false };
        var ticket = CreateAssignedTicket(customer.Id, caller.Id);
        await SeedAsync(customer, caller, ticket);
        SetBearerToken(caller with { Role = UserRole.Employee, IsActive = true });

        // Act
        var response = await client.PostAsync($"/api/tickets/{ticket.Id}/start-work", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var storedTicket = await GetTicketAsync(ticket.Id);
        storedTicket.Status.Should().Be(TicketStatus.InProgress);
        storedTicket.AssignedEmployeeUserId.Should().Be(caller.Id);
    }

    [Fact]
    public async Task PostStartWorkWithMissingTicket_ReturnsNotFound()
    {
        // Arrange
        var employee = CreateUser(UserRole.Employee);
        await SeedAsync(employee);
        SetBearerToken(employee);

        // Act
        var response = await client.PostAsync($"/api/tickets/{Guid.NewGuid()}/start-work", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostStartWorkWhenAlreadyStarted_ReturnsConflictWithoutAdditionalHistory()
    {
        // Arrange
        var customer = CreateUser(UserRole.Customer);
        var employee = CreateUser(UserRole.Employee);
        var ticket = CreateAssignedTicket(customer.Id, employee.Id);
        StartWorkCore.Execute(ticket, employee.Id, Guid.NewGuid(), DateTimeOffset.UtcNow);
        await SeedAsync(customer, employee, ticket);
        SetBearerToken(employee);

        // Act
        var response = await client.PostAsync($"/api/tickets/{ticket.Id}/start-work", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var storedTicket = await GetTicketAsync(ticket.Id);
        storedTicket.Status.Should().Be(TicketStatus.InProgress);
        storedTicket.History.Should().HaveCount(3);
    }

    [Fact]
    public async Task PostStartWorkTwice_AllowsOnlyOneWorkStartedHistoryEntry()
    {
        // Arrange
        var customer = CreateUser(UserRole.Customer);
        var employee = CreateUser(UserRole.Employee);
        var ticket = CreateAssignedTicket(customer.Id, employee.Id);
        await SeedAsync(customer, employee, ticket);
        SetBearerToken(employee);

        // Act
        var firstResponse = await client.PostAsync($"/api/tickets/{ticket.Id}/start-work", null);
        var secondResponse = await client.PostAsync($"/api/tickets/{ticket.Id}/start-work", null);

        // Assert
        firstResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var storedTicket = await GetTicketAsync(ticket.Id);
        storedTicket.History.Count(item => item.Action == TicketHistoryAction.WorkStarted).Should().Be(1);
    }

    private async Task SeedAsync(params object[] values)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ServiceDeskDbContext>();
        context.Users.AddRange(values.OfType<User>());
        context.Tickets.AddRange(values.OfType<Ticket>());
        await context.SaveChangesAsync();
    }

    private async Task<Ticket> GetTicketAsync(Guid ticketId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ServiceDeskDbContext>();
        return await context.Tickets.Include(ticket => ticket.History).SingleAsync(ticket => ticket.Id == ticketId);
    }

    private void SetBearerToken(User user)
    {
        var issuer = new JwtAccessTokenIssuer(new JwtOptions
        {
            Issuer = ServiceDeskApiFactory.JwtIssuer,
            Audience = ServiceDeskApiFactory.JwtAudience,
            SigningKey = ServiceDeskApiFactory.JwtSigningKey
        });
        var token = issuer.Issue(user, DateTimeOffset.UtcNow).Value;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private void SetEmployeeTokenWithoutSubject()
    {
        var token = new JwtSecurityToken(
            ServiceDeskApiFactory.JwtIssuer,
            ServiceDeskApiFactory.JwtAudience,
            [new Claim(ClaimTypes.Role, nameof(UserRole.Employee))],
            DateTime.UtcNow,
            DateTime.UtcNow.AddHours(1),
            new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(ServiceDeskApiFactory.JwtSigningKey)),
                SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
    }

    private static Ticket CreateAssignedTicket(Guid customerUserId, Guid employeeUserId)
    {
        var ticket = CreateTicket(customerUserId);
        AssignTicketCore.Execute(ticket, employeeUserId, Guid.NewGuid(), DateTimeOffset.UtcNow);
        return ticket;
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
