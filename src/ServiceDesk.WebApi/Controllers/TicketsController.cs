using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceDesk.Core.Tickets;
using ServiceDesk.Core.Users;
using ServiceDesk.Shell.Tickets;
using ServiceDesk.WebApi.Authentication;

namespace ServiceDesk.WebApi.Controllers;

[ApiController]
[Authorize]
[Route("api/tickets")]
public sealed class TicketsController(
    CreateTicketShell createTicketShell,
    AssignTicketShell assignTicketShell,
    StartWorkShell startWorkShell,
    GetAllTicketsShell getAllTicketsShell) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = nameof(UserRole.Employee) + "," + nameof(UserRole.Administrator))]
    [ProducesResponseType<IReadOnlyList<TicketListItemResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<TicketListItemResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var tickets = await getAllTicketsShell.ExecuteAsync(cancellationToken);
        return Ok(tickets.Select(ToListItemResponse).ToArray());
    }

    [HttpPost]
    [ProducesResponseType<TicketResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TicketResponse>> Create(
        CreateTicketHttpRequest request,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedUserId.TryGet(User, out var customerUserId))
        {
            return Forbid();
        }

        var command = new CreateTicketCommand(request.Title, request.Description, request.Priority);
        try
        {
            var ticket = await createTicketShell.ExecuteAsync(command, customerUserId, cancellationToken);
            return Created($"/api/tickets/{ticket.Id}", ToResponse(ticket));
        }
        catch (CreateTicketException exception) when (exception.Failure == CreateTicketFailureKind.CustomerNotPermitted)
        {
            return Forbid();
        }
        catch (CreateTicketException exception)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Ticket creation was rejected.",
                Detail = exception.Failure.ToString()
            });
        }
    }

    [HttpPost("{ticketId:guid}/assignment")]
    [Authorize(Roles = nameof(UserRole.Employee))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Assign(Guid ticketId, CancellationToken cancellationToken)
    {
        if (!AuthenticatedUserId.TryGet(User, out var employeeUserId))
        {
            return Forbid();
        }

        try
        {
            await assignTicketShell.ExecuteAsync(ticketId, employeeUserId, cancellationToken);
            return NoContent();
        }
        catch (AssignTicketException exception) when (exception.Failure == AssignTicketFailureKind.TicketNotFound)
        {
            return NotFound(CreateAssignmentProblemDetails("Ticket was not found."));
        }
        catch (AssignTicketException exception)
        {
            return Conflict(CreateAssignmentProblemDetails(exception.Failure.ToString()));
        }
    }

    [HttpPost("{ticketId:guid}/start-work")]
    [Authorize(Roles = nameof(UserRole.Employee))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> StartWork(Guid ticketId, CancellationToken cancellationToken)
    {
        if (!AuthenticatedUserId.TryGet(User, out var employeeUserId))
        {
            return Forbid();
        }

        try
        {
            await startWorkShell.ExecuteAsync(ticketId, employeeUserId, cancellationToken);
            return NoContent();
        }
        catch (StartWorkException exception) when (exception.Failure == StartWorkFailureKind.TicketNotFound)
        {
            return NotFound(CreateStartWorkProblemDetails("Ticket was not found."));
        }
        catch (StartWorkException exception) when (exception.Failure == StartWorkFailureKind.TicketAssignedToAnotherEmployee)
        {
            return Forbid();
        }
        catch (StartWorkException exception)
        {
            return Conflict(CreateStartWorkProblemDetails(exception.Failure.ToString()));
        }
    }

    private static TicketResponse ToResponse(Ticket ticket) => new(
        ticket.Id,
        ticket.CustomerUserId,
        ticket.Title,
        ticket.Description,
        ticket.Priority,
        ticket.Status,
        ticket.CreatedAt,
        ticket.UpdatedAt);

    private static TicketListItemResponse ToListItemResponse(TicketListItem ticket) => new(
        ticket.Id,
        ticket.CustomerEmail,
        ticket.AssignedEmployeeEmail,
        ticket.Title,
        ticket.Description,
        ticket.Priority,
        ticket.Status,
        ticket.CreatedAt,
        ticket.UpdatedAt);

    private static ProblemDetails CreateAssignmentProblemDetails(string detail) => new()
    {
        Title = "Ticket assignment was rejected.",
        Detail = detail
    };

    private static ProblemDetails CreateStartWorkProblemDetails(string detail) => new()
    {
        Title = "Starting ticket work was rejected.",
        Detail = detail
    };
}

public sealed record CreateTicketHttpRequest(
    [Required] string? Title,
    [Required] string? Description,
    TicketPriority Priority);

public sealed record TicketResponse(
    Guid Id,
    Guid CustomerUserId,
    string Title,
    string Description,
    TicketPriority Priority,
    TicketStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record TicketListItemResponse(
    Guid Id,
    string CustomerEmail,
    string? AssignedEmployeeEmail,
    string Title,
    string Description,
    TicketPriority Priority,
    TicketStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
