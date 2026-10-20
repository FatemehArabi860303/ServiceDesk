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

    public async Task<IReadOnlyList<User>> GetUsersAsync(UserFilter? filter = null, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Users.AsNoTracking();

        query = applyFilter(query, filter);

        return await query.ToListAsync(cancellationToken);
    }

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
        catch (DbUpdateException exception) when (isUniqueEmailViolation(exception))
        {
            throw new UserEmailAlreadyExistsException(exception);
        }
    }

    private static bool isUniqueEmailViolation(DbUpdateException exception)
    {
        if (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            return true;
        }

        return exception.InnerException is SqliteException { SqliteErrorCode: 19 } sqliteException
            && (sqliteException.Message.Contains("UX_Users_Email", StringComparison.Ordinal)
                || sqliteException.Message.Contains("Users.Email", StringComparison.Ordinal));
    }

    private static IQueryable<User> applyFilter(
        IQueryable<User> query,
        UserFilter? filter)
    {
        if (filter?.Role is UserRole role)
            query = query.Where(x => x.Role == role);

        return query;
    }
}
