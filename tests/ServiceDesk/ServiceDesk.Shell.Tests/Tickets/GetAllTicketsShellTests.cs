using FluentAssertions;
using NSubstitute;
using ServiceDesk.Core.Tickets;
using ServiceDesk.Shell.Tickets;

namespace ServiceDesk.Shell.Tests.Tickets;

public sealed class GetAllTicketsShellTests
{
    [Fact]
    public async Task ExecuteAsync_DelegatesToRepositoryAndReturnsTickets()
    {
        // Arrange
        var repository = Substitute.For<ITicketRepository>();
        var tickets = (IReadOnlyList<TicketListItem>)[CreateTicket(), CreateTicket()];
        repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(tickets);
        var shell = new GetAllTicketsShell(repository);

        // Act
        var result = await shell.ExecuteAsync();

        // Assert
        result.Should().BeSameAs(tickets);
        await repository.Received(1).GetAllAsync(Arg.Any<CancellationToken>());
    }

    private static TicketListItem CreateTicket() => new(
        Guid.NewGuid(),
        "customer@example.com",
        null,
        "Cannot access VPN",
        "The VPN rejects my credentials.",
        TicketPriority.High,
        TicketStatus.Open,
        DateTimeOffset.UtcNow,
        DateTimeOffset.UtcNow);
}
