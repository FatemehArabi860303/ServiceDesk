using ServiceDesk.Domain.Entities;
using ServiceDesk.UseCases.Abstractions.Persistence;
using ServiceDesk.UseCases.Common;
using ServiceDesk.UseCases.Customers.Models;

namespace ServiceDesk.UseCases.Customers;

public sealed class CreateCustomer(ICustomerRepository customerRepository)
{
    public async Task<CustomerDto> ExecuteAsync(CreateCustomerRequest request, ActorContext actor, CancellationToken cancellationToken = default)
    {
        Authorization.RequireAdministrator(actor);
        if (await customerRepository.GetByEmailAsync(request.Email, cancellationToken) is not null) throw new ConflictException("A customer with this email already exists.");
        var customer = Customer.Create(request.FirstName, request.LastName, request.Email, request.Phone);
        await customerRepository.AddAsync(customer, cancellationToken);
        return DtoMapper.ToDto(customer);
    }
}
