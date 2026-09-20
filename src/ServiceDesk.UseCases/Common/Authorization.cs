using ServiceDesk.Domain.Entities;

namespace ServiceDesk.UseCases.Common;

internal static class Authorization
{
    internal static void RequireAdministrator(ActorContext actor)
    {
        if (actor.ActorType != ActorType.Administrator) throw new ForbiddenException();
    }

    internal static void RequireCustomerOwnerOrAdministrator(Ticket ticket, ActorContext actor)
    {
        if (actor.ActorType != ActorType.Administrator && (actor.ActorType != ActorType.Customer || actor.CustomerId != ticket.CustomerId)) throw new ForbiddenException();
    }

    internal static void RequireAssignedEmployeeOrAdministrator(Ticket ticket, ActorContext actor)
    {
        if (actor.ActorType != ActorType.Administrator && (actor.ActorType != ActorType.Employee || actor.EmployeeId != ticket.AssignedEmployeeId)) throw new ForbiddenException();
    }

    internal static void RequireTicketAccess(Ticket ticket, ActorContext actor)
    {
        if (actor.ActorType == ActorType.Administrator) return;
        if (actor.ActorType == ActorType.Customer && actor.CustomerId == ticket.CustomerId) return;
        if (actor.ActorType == ActorType.Employee && actor.EmployeeId == ticket.AssignedEmployeeId) return;
        throw new ForbiddenException();
    }

    internal static void RequireTicketParticipantOrAdministrator(Ticket ticket, ActorContext actor) => RequireTicketAccess(ticket, actor);
}
