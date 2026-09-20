using ServiceDesk.Domain.Entities;
using ServiceDesk.UseCases.Abstractions.Persistence;
using ServiceDesk.UseCases.Common;
using ServiceDesk.UseCases.Customers.Models;

namespace ServiceDesk.UseCases.Customers;

public sealed class UpdateCustomer(ICustomerRepository customerRepository)
{
    public async Task<CustomerDto> ExecuteAsync(Guid customerId, UpdateCustomerRequest request, ActorContext actor, CancellationToken cancellationToken = default)
    {
        Authorization.RequireAdministrator(actor);
        var customer = await customerRepository.GetByIdAsync(customerId, cancellationToken) ?? throw new NotFoundException(nameof(Customer), customerId);
        if (!string.Equals(customer.Email, request.Email, StringComparison.OrdinalIgnoreCase))
        {
            var existing = await customerRepository.GetByEmailAsync(request.Email, cancellationToken);
            if (existing is not null && existing.Id != customerId) throw new ConflictException("A customer with this email already exists.");
        }
        customer.UpdateContactInformation(request.FirstName, request.LastName, request.Email, request.Phone);
        await customerRepository.UpdateAsync(customer, cancellationToken);
        return DtoMapper.ToDto(customer);
    }
}
