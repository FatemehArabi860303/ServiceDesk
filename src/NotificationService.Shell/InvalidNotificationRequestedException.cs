namespace NotificationService.Shell;

public sealed class InvalidNotificationRequestedException(string message) : Exception(message);
