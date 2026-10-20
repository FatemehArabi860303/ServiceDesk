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
using ServiceDesk.WebApi.Controllers;
using ServiceDesk.WebApi.IntegrationTests.Users;

namespace ServiceDesk.WebApi.IntegrationTests.Tickets;

public sealed class CreateTicketApiTests : IClassFixture<ServiceDeskApiFactory>
{
    private readonly ServiceDeskApiFactory factory;
    private readonly HttpClient client;

    public CreateTicketApiTests(ServiceDeskApiFactory factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
    }

    [Fact]
    public async Task PostWithoutJwt_ReturnsUnauthorized()
    {
        // Arrange
        var request = CreateRequest();

        // Act
        var response = await client.PostAsJsonAsync("/api/tickets", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostWithActiveCustomer_ReturnsCreatedTicketOwnedAndRecordedByCustomer()
    {
        // Arrange
        var customer = CreateUser(UserRole.Customer, isActive: true);
        await SeedUserAsync(customer);
        SetBearerToken(customer);
        var request = CreateRequest();

        // Act
        var response = await client.PostAsJsonAsync("/api/tickets", request);
        var result = await response.Content.ReadFromJsonAsync<TicketResponse>();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ServiceDeskDbContext>();
        var ticket = await context.Tickets
            .Include(storedTicket => storedTicket.History)
            .SingleAsync(storedTicket => storedTicket.Id == result!.Id);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        result.Should().NotBeNull();
        result!.CustomerUserId.Should().Be(customer.Id);
        result.Status.Should().Be(TicketStatus.Open);
        result.Priority.Should().Be(request.Priority);
        ticket.CustomerUserId.Should().Be(customer.Id);
        ticket.History.Should().ContainSingle();
        ticket.History.Single().ActorUserId.Should().Be(customer.Id);
        ticket.History.Single().Action.Should().Be(TicketHistoryAction.Created);
    }

    [Fact]
    public async Task PostWithCustomerUserIdInRequest_IgnoresSpoofedIdentity()
    {
        // Arrange
        var customer = CreateUser(UserRole.Customer, isActive: true);
        var otherCustomer = CreateUser(UserRole.Customer, isActive: true);
        await SeedUsersAsync(customer, otherCustomer);
        SetBearerToken(customer);
        var request = new
        {
            title = "Cannot access VPN",
            description = "The VPN rejects my credentials.",
            priority = TicketPriority.High,
            customerUserId = otherCustomer.Id
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/tickets", request);
        var result = await response.Content.ReadFromJsonAsync<TicketResponse>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        result.Should().NotBeNull();
        result!.CustomerUserId.Should().Be(customer.Id);
    }

    [Theory]
    [InlineData(UserRole.Employee)]
    [InlineData(UserRole.Administrator)]
    public async Task PostWithNonCustomerCaller_ReturnsForbidden(UserRole role)
    {
        // Arrange
        var caller = CreateUser(role, isActive: true);
        await SeedUserAsync(caller);
        SetBearerToken(caller);
        var request = CreateRequest();

        // Act
        var response = await client.PostAsJsonAsync("/api/tickets", request);
        var hasTicketsForCaller = await HasTicketsForCustomerAsync(caller.Id);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        hasTicketsForCaller.Should().BeFalse();
    }

    [Fact]
    public async Task PostWithInactiveCustomer_ReturnsForbidden()
    {
        // Arrange
        var customer = CreateUser(UserRole.Customer, isActive: false);
        await SeedUserAsync(customer);
        SetBearerToken(customer);
        var request = CreateRequest();

        // Act
        var response = await client.PostAsJsonAsync("/api/tickets", request);
        var hasTicketsForCustomer = await HasTicketsForCustomerAsync(customer.Id);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        hasTicketsForCustomer.Should().BeFalse();
    }

    [Fact]
    public async Task PostWithMissingPersistedCustomer_ReturnsForbidden()
    {
        // Arrange
        var customer = CreateUser(UserRole.Customer, isActive: true);
        SetBearerToken(customer);
        var request = CreateRequest();

        // Act
        var response = await client.PostAsJsonAsync("/api/tickets", request);
        var hasTicketsForCustomer = await HasTicketsForCustomerAsync(customer.Id);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        hasTicketsForCustomer.Should().BeFalse();
    }

    [Fact]
    public async Task PostWithCustomerJwtButPersistedEmployee_ReturnsForbidden()
    {
        // Arrange
        var caller = CreateUser(UserRole.Employee, isActive: true);
        await SeedUserAsync(caller);
        SetBearerToken(caller with { Role = UserRole.Customer });
        var request = CreateRequest();

        // Act
        var response = await client.PostAsJsonAsync("/api/tickets", request);
        var hasTicketsForCaller = await HasTicketsForCustomerAsync(caller.Id);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        hasTicketsForCaller.Should().BeFalse();
    }

    [Theory]
    [InlineData(" ", "The VPN rejects my credentials.", TicketPriority.High)]
    [InlineData("Cannot access VPN", " ", TicketPriority.High)]
    [InlineData("Cannot access VPN", "The VPN rejects my credentials.", (TicketPriority)99)]
    public async Task PostWithInvalidTicketRequest_ReturnsBadRequest(
        string title,
        string description,
        TicketPriority priority)
    {
        // Arrange
        var customer = CreateUser(UserRole.Customer, isActive: true);
        await SeedUserAsync(customer);
        SetBearerToken(customer);
        var request = new CreateTicketHttpRequest(title, description, priority);

        // Act
        var response = await client.PostAsJsonAsync("/api/tickets", request);
        var hasTicketsForCustomer = await HasTicketsForCustomerAsync(customer.Id);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        hasTicketsForCustomer.Should().BeFalse();
    }

    private async Task SeedUserAsync(User user) => await SeedUsersAsync(user);

    private async Task SeedUsersAsync(params User[] users)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ServiceDeskDbContext>();
        context.Users.AddRange(users);
        await context.SaveChangesAsync();
    }

    private async Task<bool> HasTicketsForCustomerAsync(Guid customerUserId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ServiceDeskDbContext>();
        return await context.Tickets.AnyAsync(ticket => ticket.CustomerUserId == customerUserId);
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

    private static CreateTicketHttpRequest CreateRequest() => new(
        "Cannot access VPN",
        "The VPN rejects my credentials.",
        TicketPriority.High);

    private static User CreateUser(UserRole role, bool isActive) => new(
        Guid.NewGuid(),
        "Ada",
        "Lovelace",
        $"ADA-{Guid.NewGuid():N}@EXAMPLE.COM".ToUpperInvariant(),
        role,
        isActive,
        DateTimeOffset.UtcNow,
        DateTimeOffset.UtcNow);
}
