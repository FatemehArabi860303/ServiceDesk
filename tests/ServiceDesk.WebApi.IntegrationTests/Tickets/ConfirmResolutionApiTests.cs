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

public sealed class ConfirmResolutionApiTests(ServiceDeskApiFactory factory) : IClassFixture<ServiceDeskApiFactory>
{
    [Fact]
    public async Task PostConfirmResolution_WithOwner_ClosesUsingJwtIdentity()
    {
        // Arrange
        using var client = factory.CreateClient();
        var (ticket, customer) = await SeedAsync();
        SetToken(client, customer);

        // Act
        var response = await client.PostAsJsonAsync($"/api/tickets/{ticket.Id}/confirm-resolution", new { actorUserId = Guid.NewGuid() });
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ServiceDeskDbContext>();
        var stored = await context.Tickets.Include(t => t.History).SingleAsync(t => t.Id == ticket.Id);
        var history = stored.History.Single(h => h.Action == TicketHistoryAction.Closed);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        stored.Status.Should().Be(TicketStatus.Closed);
        stored.ClosedAt.Should().NotBeNull().And.Be(stored.UpdatedAt);
        stored.AssignedEmployeeUserId.Should().Be(ticket.AssignedEmployeeUserId);
        stored.CreatedAt.Should().Be(ticket.CreatedAt);
        stored.CustomerUserId.Should().Be(customer.Id);
        history.ActorUserId.Should().Be(customer.Id);
        history.OccurredAt.Should().Be(stored.ClosedAt!.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("invalid-token")]
    public async Task PostConfirmResolution_WithoutValidJwt_ReturnsUnauthorized(string? token)
    {
        // Arrange
        using var client = factory.CreateClient();
        if (token is not null) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var ticketId = Guid.NewGuid();

        // Act
        var response = await client.PostAsync($"/api/tickets/{ticketId}/confirm-resolution", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.Customer)]
    [InlineData(UserRole.Employee)]
    [InlineData(UserRole.Administrator)]
    public async Task PostConfirmResolution_WithForbiddenActor_ReturnsForbidden(UserRole role)
    {
        // Arrange
        using var client = factory.CreateClient();
        var (ticket, _) = await SeedAsync();
        SetToken(client, CreateUser(role));

        // Act
        var response = await client.PostAsync($"/api/tickets/{ticket.Id}/confirm-resolution", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ServiceDeskDbContext>();
        (await context.Tickets.SingleAsync(t => t.Id == ticket.Id)).Status.Should().Be(TicketStatus.Resolved);
        (await context.TicketHistories.CountAsync(h => h.TicketId == ticket.Id)).Should().Be(4);
    }

    [Fact]
    public async Task PostConfirmResolution_WithMissingTicket_ReturnsNotFound()
    {
        // Arrange
        using var client = factory.CreateClient();
        SetToken(client, CreateUser(UserRole.Customer));
        var ticketId = Guid.NewGuid();

        // Act
        var response = await client.PostAsync($"/api/tickets/{ticketId}/confirm-resolution", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData(TicketStatus.Open)]
    [InlineData(TicketStatus.InProgress)]
    [InlineData(TicketStatus.Closed)]
    public async Task PostConfirmResolution_WithInvalidState_ReturnsConflict(TicketStatus status)
    {
        // Arrange
        using var client = factory.CreateClient();
        var (ticket, customer) = await SeedAsync(status);
        SetToken(client, customer);

        // Act
        var response = await client.PostAsync($"/api/tickets/{ticket.Id}/confirm-resolution", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task PostConfirmResolutionTwice_OnlyFirstSucceeds()
    {
        // Arrange
        using var client = factory.CreateClient();
        var (ticket, customer) = await SeedAsync();
        SetToken(client, customer);

        // Act
        var first = await client.PostAsync($"/api/tickets/{ticket.Id}/confirm-resolution", null);
        var second = await client.PostAsync($"/api/tickets/{ticket.Id}/confirm-resolution", null);

        // Assert
        first.StatusCode.Should().Be(HttpStatusCode.NoContent);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ServiceDeskDbContext>();
        (await context.TicketHistories.CountAsync(h => h.TicketId == ticket.Id && h.Action == TicketHistoryAction.Closed)).Should().Be(1);
    }

    private async Task<(Ticket Ticket, User Customer)> SeedAsync(TicketStatus status = TicketStatus.Resolved)
    {
        var customer = CreateUser(UserRole.Customer);
        var employee = CreateUser(UserRole.Employee);
        var now = DateTimeOffset.UtcNow;
        var ticket = CreateTicketCore.Execute(new CreateTicketCommand("VPN", "Cannot connect", TicketPriority.High),
            new CreateTicketFacts(true), customer.Id, Guid.NewGuid(), Guid.NewGuid(), now);
        AssignTicketCore.Execute(ticket, employee.Id, Guid.NewGuid(), now);
        StartWorkCore.Execute(ticket, employee.Id, Guid.NewGuid(), now);
        ResolveTicketCore.Execute(ticket, employee.Id, UserRole.Employee, Guid.NewGuid(), now);
        typeof(Ticket).GetProperty(nameof(Ticket.Status))!.SetValue(ticket, status);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ServiceDeskDbContext>();
        context.Users.AddRange(customer, employee);
        context.Tickets.Add(ticket);
        await context.SaveChangesAsync();
        return (ticket, customer);
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
