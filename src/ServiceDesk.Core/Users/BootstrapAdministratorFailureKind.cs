namespace ServiceDesk.Core.Users;

public enum BootstrapAdministratorFailureKind
{
    InstallationNotEmpty,
    InvalidFirstName,
    InvalidLastName,
    InvalidEmail,
    EmailUnavailable
}
