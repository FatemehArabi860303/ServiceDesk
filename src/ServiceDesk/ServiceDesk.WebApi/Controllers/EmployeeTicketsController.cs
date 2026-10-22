using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceDesk.Core.Tickets;
using ServiceDesk.Core.Users;
using ServiceDesk.Shell.Tickets;
using ServiceDesk.WebApi.Authentication;

namespace ServiceDesk.WebApi.Controllers;

[ApiController]
[Authorize(Roles = nameof(UserRole.Employee))]
[Route("api/employee/tickets")]
public sealed class EmployeeTicketsController(EmployeeWorkShell shell) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<EmployeeTicketPage>> GetPage(
        [FromQuery, Range(1, 1000000)] int page = 1,
        [FromQuery, Range(1, 50)] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!AuthenticatedUserId.TryGet(User, out Guid employeeId)) return Forbid();
        try { return Ok(await shell.GetPageAsync(employeeId, page, pageSize, cancellationToken)); }
        catch (EmployeeWorkForbiddenException) { return Forbid(); }
    }

    [HttpGet("{ticketId:guid}")]
    public async Task<ActionResult<EmployeeTicketDetails>> GetDetails(Guid ticketId, CancellationToken cancellationToken)
    {
        if (!AuthenticatedUserId.TryGet(User, out Guid employeeId)) return Forbid();
        try
        {
            EmployeeTicketDetails? details = await shell.GetDetailsAsync(employeeId, ticketId, cancellationToken);
            return details is null ? NotFound() : Ok(details);
        }
        catch (EmployeeWorkForbiddenException) { return Forbid(); }
    }

    [HttpPost("{ticketId:guid}/start-work")]
    public async Task<IActionResult> Start(Guid ticketId, CancellationToken cancellationToken)
    {
        if (!AuthenticatedUserId.TryGet(User, out Guid employeeId)) return Forbid();
        try
        {
            await shell.StartAsync(employeeId, ticketId, cancellationToken);
            return NoContent();
        }
        catch (EmployeeWorkForbiddenException) { return Forbid(); }
        catch (StartWorkException exception) when (exception.Failure == StartWorkFailureKind.TicketNotFound) { return NotFound(); }
        catch (StartWorkException) { return Conflict(new { message = "This request is no longer available to start. Refresh and review its current state." }); }
        catch (AssignTicketException) { return Conflict(new { message = "This request is no longer available to start. Refresh and review its current state." }); }
    }
}
