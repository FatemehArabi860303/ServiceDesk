using FluentAssertions;
using NSubstitute;
using ServiceDesk.Core.Tickets;
using ServiceDesk.Core.Users;
using ServiceDesk.Shell.Tickets;
using ServiceDesk.Shell.Users;

namespace ServiceDesk.Shell.Tests.Tickets;

public sealed class CreateTicketShellTests
{
    private static readonly Guid CustomerUserId = Guid.Parse("0be030ef-8b48-4b49-91fe-2f4d49e070de");

    [Fact]
    public async Task ExecuteAsync_WithActiveCustomer_PersistsAndReturnsCreatedTicket()
    {
        // Arrange
        var userRepository = Substitute.For<IUserRepository>();
        var ticketRepository = Substitute.For<ITicketRepository>();
        var customer = CreateUser(UserRole.Customer, isActive: true);
        userRepository.GetByIdAsync(CustomerUserId, Arg.Any<CancellationToken>()).Returns(customer);
        var shell = new CreateTicketShell(userRepository, ticketRepository);
        var command = new CreateTicketCommand("Cannot access VPN", "The VPN rejects my credentials.", TicketPriority.High);

        // Act
        var ticket = await shell.ExecuteAsync(command, CustomerUserId);

        // Assert
        ticket.CustomerUserId.Should().Be(CustomerUserId);
        ticket.Status.Should().Be(TicketStatus.Open);
        await ticketRepository.Received(1).AddAsync(ticket, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserIsMissing_DoesNotPersist()
    {
        // Arrange
        var userRepository = Substitute.For<IUserRepository>();
        var ticketRepository = Substitute.For<ITicketRepository>();
        userRepository.GetByIdAsync(CustomerUserId, Arg.Any<CancellationToken>()).Returns((User?)null);
        var shell = new CreateTicketShell(userRepository, ticketRepository);
        var command = new CreateTicketCommand("Cannot access VPN", "The VPN rejects my credentials.", TicketPriority.High);

        // Act
        Func<Task> act = () => shell.ExecuteAsync(command, CustomerUserId);

        // Assert
        (await act.Should().ThrowAsync<CreateTicketException>()).Which.Failure.Should().Be(CreateTicketFailureKind.CustomerNotPermitted);
        await ticketRepository.DidNotReceive().AddAsync(Arg.Any<Ticket>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserIsAnEmployee_DoesNotPersist()
    {
        // Arrange
        var userRepository = Substitute.For<IUserRepository>();
        var ticketRepository = Substitute.For<ITicketRepository>();
        userRepository.GetByIdAsync(CustomerUserId, Arg.Any<CancellationToken>()).Returns(CreateUser(UserRole.Employee, isActive: true));
        var shell = new CreateTicketShell(userRepository, ticketRepository);
        var command = new CreateTicketCommand("Cannot access VPN", "The VPN rejects my credentials.", TicketPriority.High);

        // Act
        Func<Task> act = () => shell.ExecuteAsync(command, CustomerUserId);

        // Assert
        (await act.Should().ThrowAsync<CreateTicketException>()).Which.Failure.Should().Be(CreateTicketFailureKind.CustomerNotPermitted);
        await ticketRepository.DidNotReceive().AddAsync(Arg.Any<Ticket>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenCustomerIsInactive_DoesNotPersist()
    {
        // Arrange
        var userRepository = Substitute.For<IUserRepository>();
        var ticketRepository = Substitute.For<ITicketRepository>();
        userRepository.GetByIdAsync(CustomerUserId, Arg.Any<CancellationToken>()).Returns(CreateUser(UserRole.Customer, isActive: false));
        var shell = new CreateTicketShell(userRepository, ticketRepository);
        var command = new CreateTicketCommand("Cannot access VPN", "The VPN rejects my credentials.", TicketPriority.High);

        // Act
        Func<Task> act = () => shell.ExecuteAsync(command, CustomerUserId);

        // Assert
        (await act.Should().ThrowAsync<CreateTicketException>()).Which.Failure.Should().Be(CreateTicketFailureKind.CustomerNotPermitted);
        await ticketRepository.DidNotReceive().AddAsync(Arg.Any<Ticket>(), Arg.Any<CancellationToken>());
    }

    private static User CreateUser(UserRole role, bool isActive) => new(
        CustomerUserId,
        "Ada",
        "Lovelace",
        "ADA@EXAMPLE.COM",
        role,
        isActive,
        new DateTimeOffset(2026, 9, 22, 12, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 9, 22, 12, 0, 0, TimeSpan.Zero));
}
