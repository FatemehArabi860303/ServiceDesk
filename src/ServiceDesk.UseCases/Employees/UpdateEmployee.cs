using ServiceDesk.Domain.Entities;
using ServiceDesk.UseCases.Abstractions.Persistence;
using ServiceDesk.UseCases.Common;
using ServiceDesk.UseCases.Employees.Models;

namespace ServiceDesk.UseCases.Employees;

public sealed class UpdateEmployee(IEmployeeRepository employeeRepository)
{
    public async Task<EmployeeDto> ExecuteAsync(Guid employeeId, UpdateEmployeeRequest request, ActorContext actor, CancellationToken cancellationToken = default)
    {
        Authorization.RequireAdministrator(actor);
        var employee = await employeeRepository.GetByIdAsync(employeeId, cancellationToken) ?? throw new NotFoundException(nameof(Employee), employeeId);
        if (!string.Equals(employee.Email, request.Email, StringComparison.OrdinalIgnoreCase))
        {
            var existing = await employeeRepository.GetByEmailAsync(request.Email, cancellationToken);
            if (existing is not null && existing.Id != employeeId) throw new ConflictException("An employee with this email already exists.");
        }
        employee.UpdateProfile(request.FirstName, request.LastName, request.Email);
        await employeeRepository.UpdateAsync(employee, cancellationToken);
        return DtoMapper.ToDto(employee);
    }
}
