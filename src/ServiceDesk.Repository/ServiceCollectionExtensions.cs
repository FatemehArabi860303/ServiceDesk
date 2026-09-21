using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ServiceDesk.Repository.Users;
using ServiceDesk.Shell.Users;

namespace ServiceDesk.Repository;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddServiceDeskRepository(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<ServiceDeskDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("ServiceDesk");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("Connection string 'ServiceDesk' is required.");
            }

            options.UseSqlServer(connectionString);
        });
        services.AddScoped<IUserRepository, UserRepository>();

        return services;
    }
}
