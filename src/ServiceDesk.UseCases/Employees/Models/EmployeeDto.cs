namespace ServiceDesk.UseCases.Employees.Models;
public sealed record EmployeeDto(Guid Id, string FirstName, string LastName, string Email, bool IsActive, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
