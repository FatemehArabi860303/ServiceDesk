using ServiceDesk.Domain.Entities;
using ServiceDesk.UseCases.Abstractions.Persistence;
using ServiceDesk.UseCases.Common;
using ServiceDesk.UseCases.Employees.Models;

namespace ServiceDesk.UseCases.Employees;

public sealed class GetEmployee(IEmployeeRepository employeeRepository)
{
    public async Task<EmployeeDto> ExecuteAsync(Guid employeeId, ActorContext actor, CancellationToken cancellationToken = default)
    {
        if (actor.ActorType != ActorType.Administrator && (actor.ActorType != ActorType.Employee || actor.EmployeeId != employeeId)) throw new ForbiddenException();
        var employee = await employeeRepository.GetByIdAsync(employeeId, cancellationToken) ?? throw new NotFoundException(nameof(Employee), employeeId);
        return DtoMapper.ToDto(employee);
    }
}
