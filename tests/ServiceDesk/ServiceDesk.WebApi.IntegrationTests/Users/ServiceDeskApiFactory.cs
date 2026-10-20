using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ServiceDesk.Repository;
using ServiceDesk.Repository.Authentication;
using ServiceDesk.Repository.Users;
using ServiceDesk.Shell.Authentication;
using ServiceDesk.Shell.Users;
using ServiceDesk.WebApi.Authentication;
using ServiceDesk.WebApi.IntegrationTests.Authentication;

namespace ServiceDesk.WebApi.IntegrationTests.Users;

public sealed class ServiceDeskApiFactory : WebApplicationFactory<Program>
{
    public const string JwtIssuer = "ServiceDesk.WebApi.IntegrationTests";
    public const string JwtAudience = "ServiceDesk.WebApi.IntegrationTests";
    public const string JwtSigningKey = "integration-test-signing-key-32-bytes";
    private readonly SqliteConnection connection = new("Data Source=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting(WebHostDefaults.DetailedErrorsKey, "true");
        builder.UseSetting(JwtConfigurationKey(nameof(JwtOptions.Issuer)), JwtIssuer);
        builder.UseSetting(JwtConfigurationKey(nameof(JwtOptions.Audience)), JwtAudience);
        builder.UseSetting(JwtConfigurationKey(nameof(JwtOptions.SigningKey)), JwtSigningKey);
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [JwtConfigurationKey(nameof(JwtOptions.Issuer))] = JwtIssuer,
                [JwtConfigurationKey(nameof(JwtOptions.Audience))] = JwtAudience,
                [JwtConfigurationKey(nameof(JwtOptions.SigningKey))] = JwtSigningKey
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IDbContextOptionsConfiguration<ServiceDeskDbContext>>();
            services.RemoveAll<DbContextOptions<ServiceDeskDbContext>>();
            services.RemoveAll<ServiceDeskDbContext>();
            services.RemoveAll<IUserRepository>();
            services.RemoveAll<IAuthenticationRepository>();
            services.AddDbContext<ServiceDeskDbContext>(options => options.UseSqlite(connection));
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IAuthenticationRepository, AuthenticationRepository>();
            services.AddControllers().AddApplicationPart(typeof(AuthenticationProbeController).Assembly);
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        if (connection.State != System.Data.ConnectionState.Open)
        {
            connection.Open();
        }

        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ServiceDeskDbContext>().Database.EnsureCreated();
        return host;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            connection.Dispose();
        }

        base.Dispose(disposing);
    }

    private static string JwtConfigurationKey(string propertyName) => $"{JwtOptions.SectionName}:{propertyName}";
}
