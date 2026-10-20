using ServiceDesk.AiWorker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddAiWorker(builder.Configuration);
await builder.Build().RunAsync();
