namespace ServiceDesk.Shell.Authentication;

public sealed record BootstrapAdministratorInput(
    string? FirstName,
    string? LastName,
    string? Email,
    string? Password);
