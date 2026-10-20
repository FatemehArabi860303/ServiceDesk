namespace ServiceDesk.Shell.Authentication;

public sealed record ActivateUserAccountInput(
    string? ActivationToken,
    string? Password);
