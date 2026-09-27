using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;
using ServiceDesk.Messaging;
using ServiceDesk.NotificationWorker;
using ServiceDesk.Repository;
using ServiceDesk.Shell.Notifications;

var builder = Host.CreateApplicationBuilder(args);
var rabbitMqOptions = builder.Configuration
    .GetRequiredSection(RabbitMqOptions.SectionName)
    .Get<RabbitMqOptions>()
    ?? throw new InvalidOperationException("RabbitMQ configuration is required.");
rabbitMqOptions.Validate();

builder.Services.AddServiceDeskRepository(builder.Configuration);
builder.Services.AddSingleton(rabbitMqOptions);
builder.Services.AddSingleton<IConnectionFactory>(_ => new ConnectionFactory
{
    HostName = rabbitMqOptions.HostName,
    Port = rabbitMqOptions.Port,
    VirtualHost = rabbitMqOptions.VirtualHost,
    UserName = rabbitMqOptions.UserName,
    Password = rabbitMqOptions.Password,
    DispatchConsumersAsync = true
});
builder.Services.AddSingleton<IRequestProgressPublisher, RabbitMqRequestProgressPublisher>();
builder.Services.AddScoped<ProcessOutboxShell>();
builder.Services.AddHostedService<OutboxPublisherWorker>();

await builder.Build().RunAsync();
