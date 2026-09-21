using ServiceDesk.Repository;
using ServiceDesk.Shell.Users;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddServiceDeskRepository(builder.Configuration);
builder.Services.AddScoped<CreateUserShell>();

var app = builder.Build();

app.MapControllers();

app.Run();

public partial class Program;
