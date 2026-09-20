namespace ServiceDesk.UseCases.Common;

public sealed class NotFoundException(string resourceName, Guid id)
    : Exception($"{resourceName} with identifier '{id}' was not found.");

public sealed class ForbiddenException(string message = "The actor is not permitted to perform this operation.")
    : Exception(message);

public sealed class ConflictException(string message)
    : Exception(message);

public sealed class ConcurrencyConflictException(string message = "The resource was changed by another operation.")
    : Exception(message);
