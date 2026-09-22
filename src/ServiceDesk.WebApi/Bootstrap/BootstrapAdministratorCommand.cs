using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ServiceDesk.Shell.Authentication;

namespace ServiceDesk.WebApi.Bootstrap;

public static class BootstrapAdministratorCommand
{
    public static bool IsRequested(string[] arguments) => arguments.SequenceEqual(["--bootstrap-administrator"]);

    public static BootstrapAdministratorInput CreateInput(IConfiguration configuration)
    {
        var options = configuration.GetSection(BootstrapAdministratorOptions.SectionName)
            .Get<BootstrapAdministratorOptions>() ?? new BootstrapAdministratorOptions();

        return new BootstrapAdministratorInput(
            options.FirstName,
            options.LastName,
            options.Email,
            options.Password);
    }

    public static async Task<bool> TryRunAsync(
        string[] arguments,
        IConfiguration configuration,
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        if (!IsRequested(arguments))
        {
            return false;
        }

        var input = CreateInput(configuration);
        using var scope = services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<BootstrapAdministratorShell>()
            .ExecuteAsync(input, cancellationToken);
        return true;
    }
}
