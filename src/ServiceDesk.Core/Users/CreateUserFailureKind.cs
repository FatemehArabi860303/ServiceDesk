namespace ServiceDesk.Core.Users;

public enum CreateUserFailureKind
{
    CallerNotPermitted,
    InvalidFirstName,
    InvalidLastName,
    InvalidEmail,
    UnsupportedRole,
    EmailUnavailable
}
