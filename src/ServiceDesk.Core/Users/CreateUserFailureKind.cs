namespace ServiceDesk.Core.Users;

public enum CreateUserFailureKind
{
    InvalidFirstName,
    InvalidLastName,
    InvalidEmail,
    UnsupportedRole,
    EmailUnavailable
}
