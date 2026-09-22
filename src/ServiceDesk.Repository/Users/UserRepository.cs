using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Core.Users;
using ServiceDesk.Shell.Users;

namespace ServiceDesk.Repository.Users;

public sealed class UserRepository(ServiceDeskDbContext dbContext) : IUserRepository
{
    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Users.SingleOrDefaultAsync(user => user.Id == id, cancellationToken);

    public async Task<bool> IsEmailAvailableAsync(string email, CancellationToken cancellationToken = default)
    {
        var canonicalEmail = CreateUserCore.CanonicalizeEmail(email);
        return !await dbContext.Users.AnyAsync(user => user.Email == canonicalEmail, cancellationToken);
    }

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        dbContext.Users.Add(user);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueEmailViolation(exception))
        {
            throw new UserEmailAlreadyExistsException(exception);
        }
    }

    private static bool IsUniqueEmailViolation(DbUpdateException exception)
    {
        if (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            return true;
        }

        return exception.InnerException is SqliteException { SqliteErrorCode: 19 } sqliteException
            && (sqliteException.Message.Contains("UX_Users_Email", StringComparison.Ordinal)
                || sqliteException.Message.Contains("Users.Email", StringComparison.Ordinal));
    }
}
