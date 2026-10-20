using System.Text.Json;
using System.Text.Json.Serialization;
using System.Transactions;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Core.AiAnalysis;
using ServiceDesk.Shell.AiAnalysis;

namespace ServiceDesk.Repository.AiAnalysis;

public sealed class TicketAiAnalysisRepository(
    ServiceDeskDbContext dbContext) : ITicketAiAnalysisRepository
{
    private static readonly JsonSerializerOptions ResultJsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            Converters =
            {
                new JsonStringEnumConverter(allowIntegerValues: false)
            }
        };

    public async Task<bool> IsCompletedAsync(
        Guid eventId,
        Guid ticketId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ensureIndependentTransaction();

        var completedTicketId = await dbContext.TicketAiAnalyses
            .AsNoTracking()
            .Where(analysis => analysis.EventId == eventId)
            .Select(analysis => (Guid?)analysis.TicketId)
            .SingleOrDefaultAsync(cancellationToken);

        if (completedTicketId is null)
        {
            return false;
        }

        if (completedTicketId != ticketId)
        {
            throw new InvalidOperationException(
                "The analysis event is already associated with another ticket.");
        }

        return true;
    }

    public async Task SaveAsync(
        Guid eventId,
        Guid ticketId,
        TicketAiAnalysisResult result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        cancellationToken.ThrowIfCancellationRequested();
        ensureIndependentTransaction();

        if (dbContext.ChangeTracker.HasChanges())
        {
            throw new InvalidOperationException(
                "AI analysis persistence requires a context " +
                "without pending changes.");
        }

        var analysis = new TicketAiAnalysis(
            eventId,
            ticketId,
            JsonSerializer.Serialize(result, ResultJsonOptions),
            DateTimeOffset.UtcNow);

        try
        {
            try
            {
                await using var transaction =
                    await dbContext.Database.BeginTransactionAsync(
                        cancellationToken);

                dbContext.TicketAiAnalyses.Add(analysis);

                await dbContext.SaveChangesAsync(
                    acceptAllChangesOnSuccess: false,
                    cancellationToken: cancellationToken);

                await transaction.CommitAsync(cancellationToken);
            }
            catch (DbUpdateException exception)
                when (isDuplicateAnalysisInsert(exception, analysis))
            {
                // The failed transaction has been disposed before this catch.
                dbContext.Entry(analysis).State = EntityState.Detached;

                if (!await IsCompletedAsync(
                        eventId,
                        ticketId,
                        cancellationToken))
                {
                    throw;
                }
            }
        }
        finally
        {
            // Never leave the attempted insert pending after a failure.
            dbContext.Entry(analysis).State = EntityState.Detached;
        }
    }

    private void ensureIndependentTransaction()
    {
        if (dbContext.Database.CurrentTransaction is not null
            || Transaction.Current is not null)
        {
            throw new InvalidOperationException(
                "AI analysis persistence must own its commit boundary.");
        }
    }

    private static bool isDuplicateAnalysisInsert(
        DbUpdateException exception,
        TicketAiAnalysis analysis)
    {
        if (exception.Entries.Count != 1
            || !ReferenceEquals(exception.Entries[0].Entity, analysis))
        {
            return false;
        }

        if (exception.InnerException is SqlException
            {
                Number: 2601 or 2627
            })
        {
            return true;
        }

        return exception.InnerException is SqliteException
        {
            SqliteErrorCode: 19,
            SqliteExtendedErrorCode: 1555 or 2067
        };
    }
}