namespace ServiceDesk.Shell.Notifications;

public sealed record OutboxMessageToPublish(
    Guid Id,
    string Type,
    string Payload);
