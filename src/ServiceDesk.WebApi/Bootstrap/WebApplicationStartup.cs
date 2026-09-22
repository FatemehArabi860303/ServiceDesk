using Microsoft.Extensions.Configuration;

namespace ServiceDesk.WebApi.Bootstrap;

public static class WebApplicationStartup
{
    public static async Task RunAsync(
        string[] arguments,
        IConfiguration configuration,
        IServiceProvider services,
        Func<Task> startHttpApplication,
        CancellationToken cancellationToken = default)
    {
        if (await BootstrapAdministratorCommand.TryRunAsync(arguments, configuration, services, cancellationToken))
        {
            return;
        }

        await startHttpApplication();
    }
}
