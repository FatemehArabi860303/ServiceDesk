# AI analysis worker

Run this executable separately from WebApi, the Outbox publisher and the notification worker:

```powershell
dotnet run --project src/ServiceDesk.AiWorker
```

Configure `ConnectionStrings:ServiceDesk`, `RabbitMq` and the existing `OpenAI` settings.
Supply `OpenAI__ApiKey` and `OpenAI__Model` through environment variables or a secret store;
the checked-in empty values deliberately fail options validation until configured.
Apply existing database migrations before running the worker.

The worker consumes the existing `servicedesk.ai-analysis.v1` queue with prefetch 1. It requires
the existing Outbox JSON envelope (type, content type, message ID matching payload event ID,
ticket ID and occurrence time). It acknowledges only after `ProcessTicketAiAnalysisShell`
returns following a durable commit or a completed-event lookup.

Malformed messages, processing/persistence failures and transport failures stop the worker
without acknowledging the failed delivery. Diagnose failures before manually restarting.
There is no automatic recovery or retry. Shutdown cancels handlers and waits for their cleanup
before disposing transport and scoped dependencies, even if the host shutdown deadline expires.
