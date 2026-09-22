namespace ServiceDesk.WebApi.Bootstrap;

public sealed class BootstrapAdministratorOptions
{
    public const string SectionName = "BootstrapAdministrator";

    public string? FirstName { get; init; }

    public string? LastName { get; init; }

    public string? Email { get; init; }

    public string? Password { get; init; }
}
