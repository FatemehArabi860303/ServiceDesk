using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceDesk.Core.Tickets;
using ServiceDesk.Shell.Tickets;
using ServiceDesk.WebApi.Authentication;

namespace ServiceDesk.WebApi.Controllers;

[ApiController]
[Authorize]
[Route("api/tickets")]
public sealed class TicketsController(CreateTicketShell createTicketShell) : ControllerBase
{
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

    private static TicketResponse ToResponse(Ticket ticket) => new(
        ticket.Id,
        ticket.CustomerUserId,
        ticket.Title,
        ticket.Description,
        ticket.Priority,
        ticket.Status,
        ticket.CreatedAt,
        ticket.UpdatedAt);
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
