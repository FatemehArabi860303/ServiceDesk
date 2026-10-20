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

public sealed class ResolveTicketApiTests(ServiceDeskApiFactory factory) : IClassFixture<ServiceDeskApiFactory>
{
    [Theory]
    [InlineData(UserRole.Employee)]
    [InlineData(UserRole.Administrator)]
    public async Task PostResolve_WithPermittedActor_ResolvesAndUsesJwtIdentity(UserRole role)
    {
        // Arrange
        using var client = factory.CreateClient();
        var (ticket, employee) = await SeedAsync();
        var actor = role == UserRole.Employee ? employee : CreateUser(role);
        if (role == UserRole.Administrator) await SeedUserAsync(actor);
        SetToken(client, actor);

        // Act
        var response = await client.PostAsJsonAsync($"/api/tickets/{ticket.Id}/resolve", new { actorUserId = Guid.NewGuid() });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ServiceDeskDbContext>();
        var stored = await context.Tickets.Include(t => t.History).SingleAsync(t => t.Id == ticket.Id);
        stored.Status.Should().Be(TicketStatus.Resolved);
        stored.AssignedEmployeeUserId.Should().Be(employee.Id);
        stored.CreatedAt.Should().Be(ticket.CreatedAt);
        stored.History.Single(h => h.Action == TicketHistoryAction.Resolved).ActorUserId.Should().Be(actor.Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("invalid-token")]
    public async Task PostResolve_WithoutValidJwt_ReturnsUnauthorized(string? token)
    {
        // Arrange
        using var client = factory.CreateClient();
        if (token is not null) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var ticketId = Guid.NewGuid();

        // Act
        var response = await client.PostAsync($"/api/tickets/{ticketId}/resolve", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.Customer)]
    [InlineData(UserRole.Employee)]
    public async Task PostResolve_WithForbiddenActor_ReturnsForbidden(UserRole role)
    {
        // Arrange
        using var client = factory.CreateClient();
        var (ticket, _) = await SeedAsync();
        SetToken(client, CreateUser(role));

        // Act
        var response = await client.PostAsync($"/api/tickets/{ticket.Id}/resolve", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ServiceDeskDbContext>();
        (await context.Tickets.SingleAsync(t => t.Id == ticket.Id)).Status.Should().Be(TicketStatus.InProgress);
        (await context.TicketHistories.CountAsync(h => h.TicketId == ticket.Id)).Should().Be(3);
    }

    [Fact]
    public async Task PostResolve_WithMissingTicket_ReturnsNotFound()
    {
        // Arrange
        using var client = factory.CreateClient();
        SetToken(client, CreateUser(UserRole.Employee));
        var ticketId = Guid.NewGuid();

        // Act
        var response = await client.PostAsync($"/api/tickets/{ticketId}/resolve", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData(TicketStatus.Open)]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    public async Task PostResolve_WithInvalidState_ReturnsConflict(TicketStatus status)
    {
        // Arrange
        using var client = factory.CreateClient();
        var (ticket, employee) = await SeedAsync(status);
        SetToken(client, employee);

        // Act
        var response = await client.PostAsync($"/api/tickets/{ticket.Id}/resolve", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task PostResolveTwice_OnlyFirstSucceeds()
    {
        // Arrange
        using var client = factory.CreateClient();
        var (ticket, employee) = await SeedAsync();
        SetToken(client, employee);

        // Act
        var first = await client.PostAsync($"/api/tickets/{ticket.Id}/resolve", null);
        var second = await client.PostAsync($"/api/tickets/{ticket.Id}/resolve", null);

        // Assert
        first.StatusCode.Should().Be(HttpStatusCode.NoContent);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ServiceDeskDbContext>();
        (await context.TicketHistories.CountAsync(h => h.TicketId == ticket.Id && h.Action == TicketHistoryAction.Resolved)).Should().Be(1);
    }

    private async Task<(Ticket Ticket, User Employee)> SeedAsync(TicketStatus status = TicketStatus.InProgress)
    {
        var customer = CreateUser(UserRole.Customer);
        var employee = CreateUser(UserRole.Employee);
        var now = DateTimeOffset.UtcNow;
        var ticket = CreateTicketCore.Execute(new CreateTicketCommand("VPN", "Cannot connect", TicketPriority.High),
            new CreateTicketFacts(true), customer.Id, Guid.NewGuid(), Guid.NewGuid(), now);
        AssignTicketCore.Execute(ticket, employee.Id, Guid.NewGuid(), now);
        StartWorkCore.Execute(ticket, employee.Id, Guid.NewGuid(), now);
        typeof(Ticket).GetProperty(nameof(Ticket.Status))!.SetValue(ticket, status);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ServiceDeskDbContext>();
        context.Users.AddRange(customer, employee);
        context.Tickets.Add(ticket);
        await context.SaveChangesAsync();
        return (ticket, employee);
    }

    private async Task SeedUserAsync(User user)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ServiceDeskDbContext>();
        context.Users.Add(user);
        await context.SaveChangesAsync();
    }

    private static void SetToken(HttpClient client, User user)
    {
        var issuer = new JwtAccessTokenIssuer(new JwtOptions
        {
            Issuer = ServiceDeskApiFactory.JwtIssuer,
            Audience = ServiceDeskApiFactory.JwtAudience,
            SigningKey = ServiceDeskApiFactory.JwtSigningKey
        });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", issuer.Issue(user, DateTimeOffset.UtcNow).Value);
    }

    private static User CreateUser(UserRole role) => new(Guid.NewGuid(), "Ada", "User", $"{Guid.NewGuid():N}@EXAMPLE.COM",
        role, true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
}
