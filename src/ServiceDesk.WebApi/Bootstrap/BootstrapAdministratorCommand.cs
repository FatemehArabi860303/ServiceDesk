using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ServiceDesk.Shell.Authentication;

namespace ServiceDesk.WebApi.Bootstrap;

public static class BootstrapAdministratorCommand
{
    public static bool IsRequested(string[] arguments) => arguments.SequenceEqual(["--bootstrap-administrator"]);

    public static BootstrapAdministratorInput CreateInput(IConfiguration configuration) =>
        new(
            configuration["BootstrapAdministrator:FirstName"],
            configuration["BootstrapAdministrator:LastName"],
            configuration["BootstrapAdministrator:Email"],
            configuration["BootstrapAdministrator:Password"]);

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
