using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ServiceDesk.Repository.Users;
using ServiceDesk.Repository.Authentication;
using ServiceDesk.Repository.Tickets;
using ServiceDesk.Shell.Tickets;
using ServiceDesk.Shell.Users;
using ServiceDesk.Shell.Authentication;

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
        services.AddScoped<IAuthenticationRepository, AuthenticationRepository>();
        services.AddScoped<IAdministratorBootstrapRepository, AdministratorBootstrapRepository>();
        services.AddScoped<ITicketRepository, TicketRepository>();

        return services;
    }
}
