using FluentAssertions;
using NSubstitute;
using ServiceDesk.Shell.Notifications;

namespace ServiceDesk.Shell.Tests.Notifications;

public sealed class ProcessOutboxShellTests
{
    [Fact]
    public async Task ProcessNextAsync_WhenNoMessageExists_DoesNotPublishOrMark()
    {
        // Arrange
        IOutboxRepository repository = Substitute.For<IOutboxRepository>();
        IOutboxMessagePublisher publisher =
            Substitute.For<IOutboxMessagePublisher>();

        ProcessOutboxShell shell = new(repository, publisher);

        // Act
        bool processed = await shell.ProcessNextAsync();

        // Assert
        processed.Should().BeFalse();

        await publisher.DidNotReceive().PublishAsync(
            Arg.Any<OutboxMessageToPublish>(),
            Arg.Any<CancellationToken>());

        await repository.DidNotReceive().MarkPublishedAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("notification.requested.v1")]
    [InlineData("ticket.ai-analysis.requested.v1")]
    public async Task ProcessNextAsync_WhenPublicationSucceeds_MarksUsingClaimToken(
        string eventType)
    {
        // Arrange
        IOutboxRepository repository = Substitute.For<IOutboxRepository>();
        IOutboxMessagePublisher publisher =
            Substitute.For<IOutboxMessagePublisher>();

        OutboxMessageToPublish message = new(Guid.NewGuid(), eventType, "{}");
        Guid actualClaimToken = Guid.Empty;

        repository.TryClaimNextAsync(
                Arg.Any<Guid>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                actualClaimToken = call.ArgAt<Guid>(0);
                return Task.FromResult<OutboxMessageToPublish?>(message);
            });

        repository.MarkPublishedAsync(
                message.Id,
                Arg.Any<Guid>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        ProcessOutboxShell shell = new(repository, publisher);

        // Act
        bool processed = await shell.ProcessNextAsync();

        // Assert
        processed.Should().BeTrue();
        actualClaimToken.Should().NotBeEmpty();

        await publisher.Received(1).PublishAsync(
            message,
            Arg.Any<CancellationToken>());

        await repository.Received(1).MarkPublishedAsync(
            message.Id,
            actualClaimToken,
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessNextAsync_WhilePublicationIsIncomplete_DoesNotMark()
    {
        // Arrange
        IOutboxRepository repository = Substitute.For<IOutboxRepository>();
        IOutboxMessagePublisher publisher =
            Substitute.For<IOutboxMessagePublisher>();

        OutboxMessageToPublish message = new(
            Guid.NewGuid(),
            "ticket.ai-analysis.requested.v1",
            "{}");

        TaskCompletionSource<bool> completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        repository.TryClaimNextAsync(
                Arg.Any<Guid>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<OutboxMessageToPublish?>(message));

        publisher.PublishAsync(message, Arg.Any<CancellationToken>())
            .Returns(completion.Task);

        repository.MarkPublishedAsync(
                message.Id,
                Arg.Any<Guid>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        ProcessOutboxShell shell = new(repository, publisher);

        // Act
        Task<bool> processing = shell.ProcessNextAsync();

        bool markedBeforeCompletion = repository.ReceivedCalls()
            .Any(call => call.GetMethodInfo().Name
                == nameof(IOutboxRepository.MarkPublishedAsync));

        completion.SetResult(true);
        bool processed = await processing;

        // Assert
        markedBeforeCompletion.Should().BeFalse();
        processed.Should().BeTrue();

        await repository.Received(1).MarkPublishedAsync(
            message.Id,
            Arg.Any<Guid>(),
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProcessNextAsync_WhenPublicationFails_DoesNotMark(
        bool unsupported)
    {
        // Arrange
        IOutboxRepository repository = Substitute.For<IOutboxRepository>();
        IOutboxMessagePublisher publisher =
            Substitute.For<IOutboxMessagePublisher>();

        OutboxMessageToPublish message = new(
            Guid.NewGuid(),
            "ticket.ai-analysis.requested.v1",
            "{}");

        Exception expectedException = unsupported
            ? new NotSupportedException("Unsupported event.")
            : new IOException("Broker failure or routing return.");

        repository.TryClaimNextAsync(
                Arg.Any<Guid>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<OutboxMessageToPublish?>(message));

        publisher.PublishAsync(message, Arg.Any<CancellationToken>())
            .Returns(Task.FromException(expectedException));

        ProcessOutboxShell shell = new(repository, publisher);
        Func<Task> operation = () => shell.ProcessNextAsync();

        // Act
        Exception? exception = await Record.ExceptionAsync(operation);

        // Assert
        exception.Should().BeSameAs(expectedException);

        await repository.DidNotReceive().MarkPublishedAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessNextAsync_WhenPublicationIsCanceled_DoesNotMark()
    {
        // Arrange
        IOutboxRepository repository = Substitute.For<IOutboxRepository>();
        IOutboxMessagePublisher publisher =
            Substitute.For<IOutboxMessagePublisher>();

        OutboxMessageToPublish message = new(
            Guid.NewGuid(),
            "ticket.ai-analysis.requested.v1",
            "{}");

        using CancellationTokenSource source = new();
        source.Cancel();

        repository.TryClaimNextAsync(
                Arg.Any<Guid>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<DateTimeOffset>(),
                source.Token)
            .Returns(Task.FromResult<OutboxMessageToPublish?>(message));

        publisher.PublishAsync(message, source.Token)
            .Returns(Task.FromCanceled(source.Token));

        ProcessOutboxShell shell = new(repository, publisher);
        Func<Task> operation = () => shell.ProcessNextAsync(source.Token);

        // Act
        Exception? exception = await Record.ExceptionAsync(operation);

        // Assert
        exception.Should().BeAssignableTo<OperationCanceledException>();

        await repository.DidNotReceive().MarkPublishedAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>());
    }
}