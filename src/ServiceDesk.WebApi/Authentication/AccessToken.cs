namespace ServiceDesk.WebApi.Authentication;

public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);
