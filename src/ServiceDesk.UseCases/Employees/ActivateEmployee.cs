using ServiceDesk.Domain.Entities;
using ServiceDesk.UseCases.Abstractions.Persistence;
using ServiceDesk.UseCases.Common;
using ServiceDesk.UseCases.Employees.Models;

namespace ServiceDesk.UseCases.Employees;

public sealed class ActivateEmployee(IEmployeeRepository employeeRepository)
{
    public async Task<EmployeeDto> ExecuteAsync(Guid employeeId, ActorContext actor, CancellationToken cancellationToken = default)
    {
        Authorization.RequireAdministrator(actor);
        var employee = await employeeRepository.GetByIdAsync(employeeId, cancellationToken) ?? throw new NotFoundException(nameof(Employee), employeeId);
        employee.Activate();
        await employeeRepository.UpdateAsync(employee, cancellationToken);
        return DtoMapper.ToDto(employee);
    }
}
