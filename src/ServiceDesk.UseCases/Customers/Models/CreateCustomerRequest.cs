namespace ServiceDesk.UseCases.Customers.Models;
public sealed record CreateCustomerRequest(string FirstName, string LastName, string Email, string? Phone);
