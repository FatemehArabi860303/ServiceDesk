using FluentAssertions;
using NSubstitute;
using ServiceDesk.Shell.Notifications;

namespace ServiceDesk.Shell.Tests.Notifications;

public sealed class ProcessOutboxShellTests
{
    [Fact]
    public async Task ProcessNextAsync_WhenNoMessageIsPending_DoesNotPublish()
    {
        // Arrange
        var repository = Substitute.For<IOutboxRepository>();
        var publisher = Substitute.For<IRequestProgressPublisher>();
        repository.TryClaimNextAsync(
                Arg.Any<Guid>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>())
            .Returns((OutboxMessageToPublish?)null);
        var shell = new ProcessOutboxShell(repository, publisher);

        // Act
        var processed = await shell.ProcessNextAsync();

        // Assert
        processed.Should().BeFalse();
        await publisher.DidNotReceive().PublishAsync(Arg.Any<OutboxMessageToPublish>(), Arg.Any<CancellationToken>());
        await repository.DidNotReceive().MarkPublishedAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessNextAsync_WhenPublicationIsConfirmed_MarksTheMessagePublished()
    {
        // Arrange
        var repository = Substitute.For<IOutboxRepository>();
        var publisher = Substitute.For<IRequestProgressPublisher>();
        var message = CreateMessage();
        repository.TryClaimNextAsync(
                Arg.Any<Guid>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>())
            .Returns(message);
        repository.MarkPublishedAsync(message.Id, Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));
        var shell = new ProcessOutboxShell(repository, publisher);

        // Act
        var processed = await shell.ProcessNextAsync();

        // Assert
        processed.Should().BeTrue();
        await publisher.Received(1).PublishAsync(message, Arg.Any<CancellationToken>());
        await repository.Received(1).MarkPublishedAsync(
            message.Id,
            Arg.Any<Guid>(),
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessNextAsync_WhenPublicationFails_DoesNotMarkTheMessagePublished()
    {
        // Arrange
        var repository = Substitute.For<IOutboxRepository>();
        var publisher = Substitute.For<IRequestProgressPublisher>();
        var message = CreateMessage();
        repository.TryClaimNextAsync(
                Arg.Any<Guid>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>())
            .Returns(message);
        publisher.PublishAsync(message, Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("Broker unavailable.")));
        var shell = new ProcessOutboxShell(repository, publisher);

        // Act
        Func<Task> act = () => shell.ProcessNextAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        await repository.DidNotReceive().MarkPublishedAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessNextAsync_WhenPublicationIsCancelled_DoesNotMarkTheMessagePublished()
    {
        // Arrange
        var repository = Substitute.For<IOutboxRepository>();
        var publisher = Substitute.For<IRequestProgressPublisher>();
        var message = CreateMessage();
        repository.TryClaimNextAsync(
                Arg.Any<Guid>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>())
            .Returns(message);
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();
        publisher.PublishAsync(message, cancellationSource.Token)
            .Returns(Task.FromCanceled(cancellationSource.Token));
        var shell = new ProcessOutboxShell(repository, publisher);

        // Act
        Func<Task> act = () => shell.ProcessNextAsync(cancellationSource.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        await repository.DidNotReceive().MarkPublishedAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>());
    }

    private static OutboxMessageToPublish CreateMessage() => new(
        Guid.Parse("71bbd91d-0a36-4ef9-9947-21ca9d6bc80b"),
        RequestProgressedV1.Type,
        "{\"eventId\":\"71bbd91d-0a36-4ef9-9947-21ca9d6bc80b\"}");
}
