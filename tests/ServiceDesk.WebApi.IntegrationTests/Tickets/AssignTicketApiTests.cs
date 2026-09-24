using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceDesk.Core.Tickets;
using ServiceDesk.Core.Users;
using ServiceDesk.Repository;
using ServiceDesk.WebApi.Authentication;
using ServiceDesk.WebApi.IntegrationTests.Users;

namespace ServiceDesk.WebApi.IntegrationTests.Tickets;

public sealed class AssignTicketApiTests : IClassFixture<ServiceDeskApiFactory>
{
    private readonly ServiceDeskApiFactory factory;
    private readonly HttpClient client;

    public AssignTicketApiTests(ServiceDeskApiFactory factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
    }

    [Fact]
    public async Task PostAssignmentWithoutJwt_ReturnsUnauthorized()
    {
        // Arrange
        var ticketId = Guid.NewGuid();

        // Act
        var response = await client.PostAsync($"/api/tickets/{ticketId}/assignment", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostAssignmentWithEmployeeJwt_AssignsJwtSubjectAndRecordsHistory()
    {
        // Arrange
        var customer = CreateUser(UserRole.Customer, isActive: true);
        var employee = CreateUser(UserRole.Employee, isActive: true);
        var ticket = CreateTicket(customer.Id);
        await SeedAsync(customer, employee, ticket);
        SetBearerToken(employee);

        // Act
        var response = await client.PostAsync($"/api/tickets/{ticket.Id}/assignment", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var storedTicket = await GetTicketAsync(ticket.Id);
        storedTicket.AssignedEmployeeUserId.Should().Be(employee.Id);
        storedTicket.Status.Should().Be(TicketStatus.Open);
        storedTicket.Priority.Should().Be(ticket.Priority);
        storedTicket.CreatedAt.Should().Be(ticket.CreatedAt);
        storedTicket.UpdatedAt.Should().BeAfter(ticket.UpdatedAt);
        var assignment = storedTicket.History.Single(history => history.Action == TicketHistoryAction.Assigned);
        assignment.ActorUserId.Should().Be(employee.Id);
        assignment.AssignedEmployeeUserId.Should().Be(employee.Id);
    }

    [Fact]
    public async Task PostAssignmentWithSpoofedEmployeeIdentity_IgnoresRequestBody()
    {
        // Arrange
        var customer = CreateUser(UserRole.Customer, isActive: true);
        var employee = CreateUser(UserRole.Employee, isActive: true);
        var otherEmployee = CreateUser(UserRole.Employee, isActive: true);
        var ticket = CreateTicket(customer.Id);
        await SeedAsync(customer, employee, otherEmployee, ticket);
        SetBearerToken(employee);
        var spoofedRequest = new { employeeUserId = otherEmployee.Id, assignedEmployeeUserId = otherEmployee.Id, actorUserId = otherEmployee.Id };

        // Act
        var response = await client.PostAsJsonAsync($"/api/tickets/{ticket.Id}/assignment", spoofedRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var storedTicket = await GetTicketAsync(ticket.Id);
        storedTicket.AssignedEmployeeUserId.Should().Be(employee.Id);
        storedTicket.History.Single(history => history.Action == TicketHistoryAction.Assigned).ActorUserId.Should().Be(employee.Id);
    }

    [Theory]
    [InlineData(UserRole.Customer)]
    [InlineData(UserRole.Administrator)]
    public async Task PostAssignmentWithNonEmployeeJwt_ReturnsForbidden(UserRole role)
    {
        // Arrange
        var customer = CreateUser(UserRole.Customer, isActive: true);
        var caller = CreateUser(role, isActive: true);
        var ticket = CreateTicket(customer.Id);
        await SeedAsync(customer, caller, ticket);
        SetBearerToken(caller);

        // Act
        var response = await client.PostAsync($"/api/tickets/{ticket.Id}/assignment", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var storedTicket = await GetTicketAsync(ticket.Id);
        storedTicket.AssignedEmployeeUserId.Should().BeNull();
        storedTicket.History.Should().ContainSingle();
    }

    [Fact]
    public async Task PostAssignmentWithEmployeeJwt_DoesNotReloadPersistedRoleOrActiveState()
    {
        // Arrange
        var customer = CreateUser(UserRole.Customer, isActive: true);
        var caller = CreateUser(UserRole.Customer, isActive: false);
        var ticket = CreateTicket(customer.Id);
        await SeedAsync(customer, caller, ticket);
        SetBearerToken(caller with { Role = UserRole.Employee, IsActive = true });

        // Act
        var response = await client.PostAsync($"/api/tickets/{ticket.Id}/assignment", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var storedTicket = await GetTicketAsync(ticket.Id);
        storedTicket.AssignedEmployeeUserId.Should().Be(caller.Id);
    }

    [Fact]
    public async Task PostAssignmentWithMissingTicket_ReturnsNotFound()
    {
        // Arrange
        var employee = CreateUser(UserRole.Employee, isActive: true);
        await SeedAsync(employee);
        SetBearerToken(employee);

        // Act
        var response = await client.PostAsync($"/api/tickets/{Guid.NewGuid()}/assignment", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostAssignmentWhenTicketAlreadyAssigned_ReturnsConflictWithoutAdditionalHistory()
    {
        // Arrange
        var customer = CreateUser(UserRole.Customer, isActive: true);
        var firstEmployee = CreateUser(UserRole.Employee, isActive: true);
        var secondEmployee = CreateUser(UserRole.Employee, isActive: true);
        var ticket = CreateTicket(customer.Id);
        await SeedAsync(customer, firstEmployee, secondEmployee, ticket);
        SetBearerToken(firstEmployee);
        var firstResponse = await client.PostAsync($"/api/tickets/{ticket.Id}/assignment", null);
        SetBearerToken(secondEmployee);

        // Act
        var secondResponse = await client.PostAsync($"/api/tickets/{ticket.Id}/assignment", null);

        // Assert
        firstResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var storedTicket = await GetTicketAsync(ticket.Id);
        storedTicket.AssignedEmployeeUserId.Should().Be(firstEmployee.Id);
        storedTicket.History.Should().HaveCount(2);
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

    private static Ticket CreateTicket(Guid customerUserId) => CreateTicketCore.Execute(
        new CreateTicketCommand("Cannot access VPN", "The VPN rejects my credentials.", TicketPriority.High),
        new CreateTicketFacts(true),
        customerUserId,
        Guid.NewGuid(),
        Guid.NewGuid(),
        DateTimeOffset.UtcNow);

    private static User CreateUser(UserRole role, bool isActive) => new(
        Guid.NewGuid(),
        "Ada",
        "Lovelace",
        $"ADA-{Guid.NewGuid():N}@EXAMPLE.COM",
        role,
        isActive,
        DateTimeOffset.UtcNow,
        DateTimeOffset.UtcNow);
}
