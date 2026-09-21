namespace ServiceDesk.Shell.Users;

public sealed class UserEmailAlreadyExistsException : Exception
{
    public UserEmailAlreadyExistsException(Exception innerException)
        : base("A User with this email already exists.", innerException)
    {
    }
}
