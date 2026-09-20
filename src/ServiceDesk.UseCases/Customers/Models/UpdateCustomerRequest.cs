namespace ServiceDesk.UseCases.Customers.Models;
public sealed record UpdateCustomerRequest(string FirstName, string LastName, string Email, string? Phone);
