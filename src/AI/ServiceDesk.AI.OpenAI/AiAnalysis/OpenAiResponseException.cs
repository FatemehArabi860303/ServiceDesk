namespace ServiceDesk.AI.AiAnalysis;

public sealed class OpenAiResponseException(
    string message,
    Exception? innerException = null) : Exception(message, innerException)
{
}