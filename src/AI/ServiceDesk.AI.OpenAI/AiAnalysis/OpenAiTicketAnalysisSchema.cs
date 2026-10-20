using System.Text.Json;
using ServiceDesk.Core.AiAnalysis;
using ServiceDesk.Core.Tickets;

namespace ServiceDesk.AI.AiAnalysis;

internal static class OpenAiTicketAnalysisSchema
{
    public static JsonElement Value { get; } =
        JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                summary = new { type = "string" },
                suggestedCategory = new { type = "string" },
                suggestedPriority = new
                {
                    type = "string",
                    @enum = Enum.GetNames<TicketPriority>()
                },
                priorityReason = new { type = "string" },
                troubleshootingSuggestions = new
                {
                    type = "array",
                    items = new { type = "string" }
                },
                missingInformation = new
                {
                    type = "array",
                    items = new { type = "string" }
                },
                confidence = new
                {
                    type = "string",
                    @enum = Enum.GetNames<TicketAiAnalysisConfidence>()
                }
            },
            required = new[]
            {
                "summary",
                "suggestedCategory",
                "suggestedPriority",
                "priorityReason",
                "troubleshootingSuggestions",
                "missingInformation",
                "confidence"
            },
            additionalProperties = false
        });
}