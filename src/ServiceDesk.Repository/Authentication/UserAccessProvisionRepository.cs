using System.Data;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Core.Users;
using ServiceDesk.Shell.Authentication;

namespace ServiceDesk.Repository.Authentication;

public sealed class UserAccessProvisionRepository(ServiceDeskDbContext dbContext) : IUserAccessProvisionRepository
{
    public Task<UserAccessProvision?> FindByActivationTokenHashAsync(
        byte[] activationTokenHash,
        CancellationToken cancellationToken = default) =>
        dbContext.UserAccessProvisions
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.ActivationTokenHash.SequenceEqual(activationTokenHash),
                cancellationToken);

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

    public async Task<bool> TryActivateAsync(
        UserCredential credential,
        byte[] activationTokenHash,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var provision = await dbContext.UserAccessProvisions
            .SingleOrDefaultAsync(
                candidate => candidate.ActivationTokenHash.SequenceEqual(activationTokenHash),
                cancellationToken);

        if (provision is null
            || provision.UserId != credential.UserId
            || provision.ExpiresAt <= now)
        {
            return false;
        }

        var user = await dbContext.Users
            .SingleOrDefaultAsync(candidate => candidate.Id == credential.UserId, cancellationToken);
        if (user is not { IsActive: true }
            || await dbContext.UserCredentials.AnyAsync(candidate => candidate.UserId == credential.UserId, cancellationToken))
        {
            return false;
        }

        dbContext.UserCredentials.Add(credential);
        dbContext.UserAccessProvisions.Remove(provision);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            return false;
        }
    }
}
