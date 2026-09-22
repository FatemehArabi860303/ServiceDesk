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

    private static User CreateCustomer() => new(
        CustomerUserId,
        "Ada",
        "Lovelace",
        "ADA@EXAMPLE.COM",
        UserRole.Customer,
        true,
        new DateTimeOffset(2026, 9, 22, 12, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 9, 22, 12, 0, 0, TimeSpan.Zero));

    private static Ticket CreateTicket(Guid customerUserId) =>
        CreateTicketCore.Execute(
            new CreateTicketCommand("Cannot access VPN", "The VPN rejects my credentials.", TicketPriority.High),
            new CreateTicketFacts(true),
            customerUserId,
            Guid.Parse("247da287-7f60-4111-bbbf-cde1157402ef"),
            Guid.Parse("4e5bb9c5-a15a-4d8b-a0e0-1f8624ecb01d"),
            new DateTimeOffset(2026, 9, 22, 12, 0, 0, TimeSpan.Zero));
}
