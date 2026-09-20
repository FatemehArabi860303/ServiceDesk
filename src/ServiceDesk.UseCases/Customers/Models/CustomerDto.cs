namespace ServiceDesk.UseCases.Customers.Models;
public sealed record CustomerDto(Guid Id, string FirstName, string LastName, string Email, string? Phone, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
