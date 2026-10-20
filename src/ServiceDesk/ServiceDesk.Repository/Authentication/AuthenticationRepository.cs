using Microsoft.EntityFrameworkCore;
using ServiceDesk.Shell.Authentication;

namespace ServiceDesk.Repository.Authentication;

public sealed class AuthenticationRepository(ServiceDeskDbContext dbContext) : IAuthenticationRepository
{
    public async Task<AuthenticationUser?> FindByCanonicalEmailAsync(
        string canonicalEmail,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users
            .SingleOrDefaultAsync(candidate => candidate.Email == canonicalEmail, cancellationToken);

        if (user is null)
        {
            return null;
        }

        var credential = await dbContext.UserCredentials
            .SingleOrDefaultAsync(candidate => candidate.UserId == user.Id, cancellationToken);

        return new AuthenticationUser(user, credential);
    }
}
