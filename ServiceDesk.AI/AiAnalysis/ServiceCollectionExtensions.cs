using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ServiceDesk.AI.Configuration;
using ServiceDesk.Shell.AiAnalysis;

namespace ServiceDesk.AI.AiAnalysis;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOpenAiAnalysis(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton<
            IValidateOptions<OpenAiOptions>,
            OpenAiOptionsValidator>();

        services.AddOptions<OpenAiOptions>()
            .Bind(configuration.GetSection(OpenAiOptions.SectionName))
            .ValidateOnStart();

        services.AddHttpClient<ITicketAiAnalyzer, OpenAiTicketAnalyzer>(
            (serviceProvider, client) =>
            {
                var options = serviceProvider
                    .GetRequiredService<IOptions<OpenAiOptions>>()
                    .Value;

                client.BaseAddress = new Uri(
                    options.BaseUrl,
                    UriKind.Absolute);
            });

        return services;
    }
}
