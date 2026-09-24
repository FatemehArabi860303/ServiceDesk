using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using ServiceDesk.Core.Users;
using ServiceDesk.Repository;
using ServiceDesk.Shell.Authentication;
using ServiceDesk.Shell.Tickets;
using ServiceDesk.Shell.Users;
using ServiceDesk.WebApi.Authentication;
using ServiceDesk.WebApi.Bootstrap;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddServiceDeskRepository(builder.Configuration);
var isBootstrapCommand = ServiceDesk.WebApi.Bootstrap.BootstrapAdministratorCommand.IsRequested(args);

if (!isBootstrapCommand)
{
    var jwtOptions = JwtOptions.BindAndValidate(builder.Configuration);
    builder.Services.AddSingleton(jwtOptions);
    builder.Services.AddSingleton<JwtAccessTokenIssuer>();
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = jwtOptions.CreateSigningKey(),
                ValidateIssuer = true,
                ValidIssuer = jwtOptions.Issuer,
                ValidateAudience = true,
                ValidAudience = jwtOptions.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,
                RoleClaimType = ClaimTypes.Role
            };
        });
    builder.Services.AddAuthorization();
}

builder.Services.AddScoped<CreateUserShell>();
builder.Services.AddScoped<BootstrapAdministratorShell>();
builder.Services.AddScoped<AuthenticateUserShell>();
builder.Services.AddScoped<ProvisionUserAccessShell>();
builder.Services.AddScoped<ActivateUserAccountShell>();
builder.Services.AddScoped<CreateTicketShell>();
builder.Services.AddScoped<AssignTicketShell>();
builder.Services.AddScoped<StartWorkShell>();
builder.Services.AddScoped<GetAllTicketsShell>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

var app = builder.Build();

await WebApplicationStartup.RunAsync(args, builder.Configuration, app.Services, () =>
{
    if (!isBootstrapCommand)
    {
        app.UseAuthentication();
        app.UseAuthorization();
    }

    app.MapControllers();
    return app.RunAsync();
});

public partial class Program;
