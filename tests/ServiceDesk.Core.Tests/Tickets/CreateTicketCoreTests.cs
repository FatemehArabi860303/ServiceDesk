using FluentAssertions;
using ServiceDesk.Core.Tickets;

namespace ServiceDesk.Core.Tests.Tickets;

public sealed class CreateTicketCoreTests
{
    private static readonly Guid CustomerUserId = Guid.Parse("0be030ef-8b48-4b49-91fe-2f4d49e070de");
    private static readonly Guid TicketId = Guid.Parse("247da287-7f60-4111-bbbf-cde1157402ef");
    private static readonly Guid HistoryId = Guid.Parse("4e5bb9c5-a15a-4d8b-a0e0-1f8624ecb01d");
    private static readonly DateTimeOffset Now = new(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Execute_WithValidCommand_CreatesOpenUnassignedTicketWithCreationHistory()
    {
        // Arrange
        var command = new CreateTicketCommand("Cannot access VPN", "The VPN rejects my credentials.", TicketPriority.High);

        // Act
        var ticket = Execute(command);

        // Assert
        ticket.Id.Should().Be(TicketId);
        ticket.CustomerUserId.Should().Be(CustomerUserId);
        ticket.Title.Should().Be(command.Title);
        ticket.Description.Should().Be(command.Description);
        ticket.Priority.Should().Be(TicketPriority.High);
        ticket.Status.Should().Be(TicketStatus.Open);
        ticket.CreatedAt.Should().Be(Now);
        ticket.UpdatedAt.Should().Be(Now);
        ticket.History.Should().ContainSingle();
        var history = ticket.History.Single();
        history.Id.Should().Be(HistoryId);
        history.TicketId.Should().Be(ticket.Id);
        history.ActorUserId.Should().Be(ticket.CustomerUserId);
        history.Action.Should().Be(TicketHistoryAction.Created);
        history.OccurredAt.Should().Be(ticket.CreatedAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Execute_WithInvalidTitle_Throws(string? title)
    {
        // Arrange
        var command = new CreateTicketCommand(title, "The VPN rejects my credentials.", TicketPriority.High);

        // Act
        Action act = () => Execute(command);

        // Assert
        act.Should().Throw<CreateTicketException>().Which.Failure.Should().Be(CreateTicketFailureKind.InvalidTitle);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Execute_WithInvalidDescription_Throws(string? description)
    {
        // Arrange
        var command = new CreateTicketCommand("Cannot access VPN", description, TicketPriority.High);

        // Act
        Action act = () => Execute(command);

        // Assert
        act.Should().Throw<CreateTicketException>().Which.Failure.Should().Be(CreateTicketFailureKind.InvalidDescription);
    }

    [Fact]
    public void Execute_WithUnsupportedPriority_Throws()
    {
        // Arrange
        var command = new CreateTicketCommand("Cannot access VPN", "The VPN rejects my credentials.", (TicketPriority)99);

        // Act
        Action act = () => Execute(command);

        // Assert
        act.Should().Throw<CreateTicketException>().Which.Failure.Should().Be(CreateTicketFailureKind.InvalidPriority);
    }

    [Fact]
    public void Execute_WhenCustomerIsNotPermitted_Throws()
    {
        // Arrange
        var command = new CreateTicketCommand("Cannot access VPN", "The VPN rejects my credentials.", TicketPriority.High);
        var facts = new CreateTicketFacts(false);

        // Act
        Action act = () => Execute(command, facts);

        // Assert
        act.Should().Throw<CreateTicketException>().Which.Failure.Should().Be(CreateTicketFailureKind.CustomerNotPermitted);
    }

    private static Ticket Execute(CreateTicketCommand command, CreateTicketFacts? facts = null) =>
        CreateTicketCore.Execute(command, facts ?? new CreateTicketFacts(true), CustomerUserId, TicketId, HistoryId, Now);
}
