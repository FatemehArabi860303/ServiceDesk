using ServiceDesk.Core.Users;

namespace ServiceDesk.Shell.Users;

public sealed record UserFilter(UserRole? Role = null);
