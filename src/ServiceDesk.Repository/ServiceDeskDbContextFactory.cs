using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ServiceDesk.Repository;

public sealed class ServiceDeskDbContextFactory : IDesignTimeDbContextFactory<ServiceDeskDbContext>
{
    public ServiceDeskDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ServiceDeskDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=ServiceDeskDesignTime;Trusted_Connection=True;MultipleActiveResultSets=true")
            .Options;

        return new ServiceDeskDbContext(options);
    }
}
