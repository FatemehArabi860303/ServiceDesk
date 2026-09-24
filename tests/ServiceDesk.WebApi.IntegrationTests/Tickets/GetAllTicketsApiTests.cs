using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceDesk.Core.Tickets;
using ServiceDesk.Core.Users;
using ServiceDesk.Repository;
using ServiceDesk.WebApi.Authentication;
using ServiceDesk.WebApi.Controllers;
using ServiceDesk.WebApi.IntegrationTests.Users;

namespace ServiceDesk.WebApi.IntegrationTests.Tickets;

public sealed class GetAllTicketsApiTests : IClassFixture<ServiceDeskApiFactory>
{
    private readonly ServiceDeskApiFactory factory;
    private readonly HttpClient client;

    public GetAllTicketsApiTests(ServiceDeskApiFactory factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
    }

    [Fact]
    public async Task GetWithoutJwt_ReturnsUnauthorized()
    {
        // Arrange
        await ClearTicketsAsync();

        // Act
        var response = await client.GetAsync("/api/tickets");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetWithInvalidJwt_ReturnsUnauthorized()
    {
        // Arrange
        await ClearTicketsAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-valid-token");

        // Act
        var response = await client.GetAsync("/api/tickets");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetWithEmployee_ReturnsAllTicketsWithCustomerAndAssignedEmployeeEmails()
    {
        // Arrange
        await ClearTicketsAsync();
        var customer = CreateUser(UserRole.Customer, "customer@example.com");
        var employee = CreateUser(UserRole.Employee, "employee@example.com");
        var openTicket = CreateTicket(customer.Id, TicketPriority.Low);
        var assignedTicket = CreateTicket(customer.Id, TicketPriority.Critical);
        AssignTicketCore.Execute(assignedTicket, employee.Id, Guid.NewGuid(), DateTimeOffset.UtcNow);
        await SeedUsersAsync(customer, employee);
        await SeedTicketsAsync(openTicket, assignedTicket);
        SetBearerToken(employee);

        // Act
        var response = await client.GetAsync("/api/tickets");
        var responseBody = await response.Content.ReadAsStringAsync();
        var tickets = JsonSerializer.Deserialize<TicketListItemResponse[]>(responseBody, JsonSerializerOptions.Web);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        tickets.Should().NotBeNull();
        tickets.Should().HaveCount(2);
        tickets.Should().Contain(ticket => ticket.Id == openTicket.Id
            && ticket.CustomerEmail == customer.Email
            && ticket.AssignedEmployeeEmail == null
            && ticket.Title == openTicket.Title
            && ticket.Description == openTicket.Description
            && ticket.Priority == openTicket.Priority
            && ticket.Status == openTicket.Status
            && ticket.CreatedAt == openTicket.CreatedAt
            && ticket.UpdatedAt == openTicket.UpdatedAt);
        tickets.Should().Contain(ticket => ticket.Id == assignedTicket.Id
            && ticket.CustomerEmail == customer.Email
            && ticket.AssignedEmployeeEmail == employee.Email
            && ticket.Title == assignedTicket.Title
            && ticket.Description == assignedTicket.Description
            && ticket.Priority == assignedTicket.Priority
            && ticket.Status == assignedTicket.Status
            && ticket.CreatedAt == assignedTicket.CreatedAt
            && ticket.UpdatedAt == assignedTicket.UpdatedAt);
        responseBody.Should().NotContain("customerUserId");
        responseBody.Should().NotContain("assignedEmployeeUserId");
    }

    [Fact]
    public async Task GetWithAdministrator_ReturnsAllTickets()
    {
        // Arrange
        await ClearTicketsAsync();
        var customer = CreateUser(UserRole.Customer, "customer@example.com");
        var administrator = CreateUser(UserRole.Administrator, "administrator@example.com");
        var ticket = CreateTicket(customer.Id, TicketPriority.Medium);
        await SeedUsersAsync(customer, administrator);
        await SeedTicketsAsync(ticket);
        SetBearerToken(administrator);

        // Act
        var response = await client.GetAsync("/api/tickets");
        var tickets = await response.Content.ReadFromJsonAsync<TicketListItemResponse[]>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        tickets.Should().ContainSingle(item => item.Id == ticket.Id
            && item.CustomerEmail == customer.Email
            && item.AssignedEmployeeEmail == null);
    }

    [Fact]
    public async Task GetWithCustomer_ReturnsForbidden()
    {
        // Arrange
        await ClearTicketsAsync();
        var customer = CreateUser(UserRole.Customer, "customer@example.com");
        await SeedUsersAsync(customer);
        SetBearerToken(customer);

        // Act
        var response = await client.GetAsync("/api/tickets");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetWithEmployeeAndNoTickets_ReturnsEmptyList()
    {
        // Arrange
        await ClearTicketsAsync();
        var employee = CreateUser(UserRole.Employee, "employee@example.com");
        await SeedUsersAsync(employee);
        SetBearerToken(employee);

        // Act
        var response = await client.GetAsync("/api/tickets");
        var tickets = await response.Content.ReadFromJsonAsync<TicketListItemResponse[]>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        tickets.Should().NotBeNull();
        tickets.Should().BeEmpty();
    }

    private async Task ClearTicketsAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ServiceDeskDbContext>();
        await context.Tickets.ExecuteDeleteAsync();
    }

    private async Task SeedUsersAsync(params User[] users)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ServiceDeskDbContext>();
        context.Users.AddRange(users);
        await context.SaveChangesAsync();
    }

    private async Task SeedTicketsAsync(params Ticket[] tickets)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ServiceDeskDbContext>();
        context.Tickets.AddRange(tickets);
        await context.SaveChangesAsync();
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

    private static Ticket CreateTicket(Guid customerUserId, TicketPriority priority) => CreateTicketCore.Execute(
        new CreateTicketCommand("Cannot access VPN", "The VPN rejects my credentials.", priority),
        new CreateTicketFacts(true),
        customerUserId,
        Guid.NewGuid(),
        Guid.NewGuid(),
        DateTimeOffset.UtcNow);

    private static User CreateUser(UserRole role, string emailPrefix) => new(
        Guid.NewGuid(),
        "Ada",
        "Lovelace",
        $"{emailPrefix}-{Guid.NewGuid():N}@example.com",
        role,
        true,
        DateTimeOffset.UtcNow,
        DateTimeOffset.UtcNow);
}
