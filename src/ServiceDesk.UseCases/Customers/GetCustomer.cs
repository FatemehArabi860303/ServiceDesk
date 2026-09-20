using ServiceDesk.Domain.Entities;
using ServiceDesk.UseCases.Abstractions.Persistence;
using ServiceDesk.UseCases.Common;
using ServiceDesk.UseCases.Customers.Models;

namespace ServiceDesk.UseCases.Customers;

public sealed class GetCustomer(ICustomerRepository customerRepository)
{
    public async Task<CustomerDto> ExecuteAsync(Guid customerId, ActorContext actor, CancellationToken cancellationToken = default)
    {
        if (actor.ActorType != ActorType.Administrator && actor.ActorType != ActorType.Employee && (actor.ActorType != ActorType.Customer || actor.CustomerId != customerId)) throw new ForbiddenException();
        var customer = await customerRepository.GetByIdAsync(customerId, cancellationToken) ?? throw new NotFoundException(nameof(Customer), customerId);
        return DtoMapper.ToDto(customer);
    }
}
