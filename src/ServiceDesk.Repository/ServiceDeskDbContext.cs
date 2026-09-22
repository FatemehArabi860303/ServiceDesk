using Microsoft.EntityFrameworkCore;
using ServiceDesk.Core.Tickets;
using ServiceDesk.Core.Users;

namespace ServiceDesk.Repository;

public sealed class ServiceDeskDbContext(DbContextOptions<ServiceDeskDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Ticket> Tickets => Set<Ticket>();

    public DbSet<TicketHistory> TicketHistories => Set<TicketHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ServiceDeskDbContext).Assembly);
    }
}
