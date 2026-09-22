using ServiceDesk.Repository;
using Microsoft.AspNetCore.Identity;
using ServiceDesk.Core.Users;
using ServiceDesk.Shell.Authentication;
using ServiceDesk.Shell.Users;
using ServiceDesk.WebApi.Bootstrap;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddServiceDeskRepository(builder.Configuration);
builder.Services.AddScoped<CreateUserShell>();
builder.Services.AddScoped<BootstrapAdministratorShell>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

var app = builder.Build();

await WebApplicationStartup.RunAsync(args, builder.Configuration, app.Services, () =>
{
    app.MapControllers();
    return app.RunAsync();
});

public partial class Program;
