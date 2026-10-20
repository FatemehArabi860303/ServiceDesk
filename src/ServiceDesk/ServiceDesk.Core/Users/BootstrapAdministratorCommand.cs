namespace ServiceDesk.Core.Users;

public sealed record BootstrapAdministratorCommand(string? FirstName, string? LastName, string? Email);
