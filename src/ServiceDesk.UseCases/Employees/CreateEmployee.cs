using ServiceDesk.Domain.Entities;
using ServiceDesk.UseCases.Abstractions.Persistence;
using ServiceDesk.UseCases.Common;
using ServiceDesk.UseCases.Employees.Models;

namespace ServiceDesk.UseCases.Employees;

public sealed class CreateEmployee(IEmployeeRepository employeeRepository)
{
    public async Task<EmployeeDto> ExecuteAsync(CreateEmployeeRequest request, ActorContext actor, CancellationToken cancellationToken = default)
    {
        Authorization.RequireAdministrator(actor);
        if (await employeeRepository.GetByEmailAsync(request.Email, cancellationToken) is not null) throw new ConflictException("An employee with this email already exists.");
        var employee = Employee.Create(request.FirstName, request.LastName, request.Email);
        await employeeRepository.AddAsync(employee, cancellationToken);
        return DtoMapper.ToDto(employee);
    }
}
