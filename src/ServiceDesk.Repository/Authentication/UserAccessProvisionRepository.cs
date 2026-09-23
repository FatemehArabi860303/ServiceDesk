using System.Data;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Shell.Authentication;

namespace ServiceDesk.Repository.Authentication;

public sealed class UserAccessProvisionRepository(ServiceDeskDbContext dbContext) : IUserAccessProvisionRepository
{
    public Task<bool> HasCredentialAsync(Guid userId, CancellationToken cancellationToken = default) =>
        dbContext.UserCredentials.AnyAsync(credential => credential.UserId == userId, cancellationToken);

    public async Task ReplaceAsync(UserAccessProvision provision, CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var existing = await dbContext.UserAccessProvisions
            .SingleOrDefaultAsync(candidate => candidate.UserId == provision.UserId, cancellationToken);

        if (existing is not null)
        {
            dbContext.UserAccessProvisions.Remove(existing);
        }

        dbContext.UserAccessProvisions.Add(provision);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
