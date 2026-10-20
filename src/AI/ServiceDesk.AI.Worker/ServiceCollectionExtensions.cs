using RabbitMQ.Client;
using RabbitMq.Consumers;
using ServiceDesk.AI.AiAnalysis;
using ServiceDesk.Messaging;
using ServiceDesk.Repository;
using ServiceDesk.Shell.AiAnalysis;

namespace ServiceDesk.AiWorker;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAiWorker(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetRequiredSection(RabbitMqOptions.SectionName).Get<RabbitMqOptions>()
            ?? throw new InvalidOperationException("RabbitMQ configuration is required.");
        options.Validate();
        services.AddServiceDeskRepository(configuration);
        services.AddOpenAiAnalysis(configuration);
        services.AddScoped<AnalyzeRequestShell>();
        services.AddScoped<ProcessTicketAiAnalysisShell>();
        services.AddScoped<TicketAiAnalysisMessageHandler>();
        services.AddSingleton(TicketAiAnalysisMessageHandler.ConsumerOptions);
        services.AddSingleton<IConnectionFactory>(_ => new ConnectionFactory
        {
            HostName = options.HostName,
            Port = options.Port,
            VirtualHost = options.VirtualHost,
            UserName = options.UserName,
            Password = options.Password,
            DispatchConsumersAsync = true,
            AutomaticRecoveryEnabled = false
        });
        services.AddSingleton<RabbitMqConsumer<TicketAiAnalysisMessageHandler>>();
        services.AddHostedService<AiAnalysisConsumerWorker>();
        return services;
    }
}
