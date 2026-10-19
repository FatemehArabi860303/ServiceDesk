using Microsoft.Extensions.Configuration;
using NotificationService.Email;
using NotificationService.Messaging;
using NotificationService.Shell;
using NotificationService.Worker;
using RabbitMQ.Client;
using RabbitMq.Consumers;

var builder = Host.CreateApplicationBuilder(args);

var rabbitMqOptions = builder.Configuration
    .GetRequiredSection(RabbitMqNotificationOptions.SectionName)
    .Get<RabbitMqNotificationOptions>()
    ?? throw new InvalidOperationException("RabbitMQ configuration is required.");
rabbitMqOptions.Validate();

var smtpOptions = builder.Configuration
    .GetRequiredSection(SmtpOptions.SectionName)
    .Get<SmtpOptions>()
    ?? throw new InvalidOperationException("SMTP configuration is required.");
smtpOptions.Validate();

builder.Services.AddSingleton(rabbitMqOptions);
builder.Services.AddSingleton(smtpOptions);
builder.Services.AddSingleton<IConnectionFactory>(_ => new ConnectionFactory
{
    HostName = rabbitMqOptions.HostName,
    Port = rabbitMqOptions.Port,
    VirtualHost = rabbitMqOptions.VirtualHost,
    UserName = rabbitMqOptions.UserName,
    Password = rabbitMqOptions.Password,
    DispatchConsumersAsync = true,
    AutomaticRecoveryEnabled = false
});
builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<ProcessNotificationShell>();
builder.Services.AddScoped<NotificationMessageHandler>();
builder.Services.AddSingleton(NotificationMessageHandler.ConsumerOptions);
builder.Services.AddSingleton<RabbitMqConsumer<NotificationMessageHandler>>();
builder.Services.AddHostedService<NotificationConsumerWorker>();

await builder.Build().RunAsync();
