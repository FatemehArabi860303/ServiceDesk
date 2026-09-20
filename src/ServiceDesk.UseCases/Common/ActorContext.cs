namespace ServiceDesk.UseCases.Common;

public sealed record ActorContext(
    string ActorReference,
    ActorType ActorType,
    Guid? CustomerId = null,
    Guid? EmployeeId = null);
