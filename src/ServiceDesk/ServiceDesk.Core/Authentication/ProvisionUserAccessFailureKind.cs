namespace ServiceDesk.Core.Authentication;

public enum ProvisionUserAccessFailureKind
{
    CallerNotPermitted,
    TargetNotFound,
    TargetInactive,
    TargetAlreadyCredentialed
}
