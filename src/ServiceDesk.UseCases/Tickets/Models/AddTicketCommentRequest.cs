namespace ServiceDesk.UseCases.Tickets.Models;
public sealed record AddTicketCommentRequest(string Comment, string ConcurrencyToken);
