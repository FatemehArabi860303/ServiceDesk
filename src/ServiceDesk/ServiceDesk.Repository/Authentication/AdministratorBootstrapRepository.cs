using System.Data;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Core.Users;
using ServiceDesk.Shell.Authentication;

namespace ServiceDesk.Repository.Authentication;

public sealed class AdministratorBootstrapRepository(ServiceDeskDbContext dbContext) : IAdministratorBootstrapRepository
{
    public async Task<bool> IsInstallationEmptyAsync(CancellationToken cancellationToken = default) =>
        !await dbContext.Users.AnyAsync(cancellationToken);

    public async Task<bool> TryAddAsync(
        User administrator,
        UserCredential credential,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        if (await dbContext.Users.AnyAsync(cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        dbContext.Users.Add(administrator);
        dbContext.UserCredentials.Add(credential);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}
