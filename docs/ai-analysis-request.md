# AI-Assisted Customer Request Analysis

## Submission Boundary

Customer ticket submission depends on ticket business validation and the
ticket database transaction.

1. WebApi invokes CreateTicketShell.
2. CreateTicketShell invokes the existing pure CreateTicketCore.
3. Shell requests atomic persistence through a repository abstraction.
4. Repository commits the ticket and TicketAiAnalysisRequestedV1 Outbox entry
   in the same database transaction.
5. After successful persistence, WebApi returns success and the ticket ID.

Ticket submission does not invoke AI analysis, call OpenAI, publish directly
to RabbitMQ, or start fire-and-forget background tasks.

A failed ticket database transaction fails ticket submission.

## Publication Boundary

ServiceDesk.Outbox.Worker hosts the existing Outbox publisher.

The publisher delivers committed Outbox events to RabbitMQ asynchronously.
An event remains available for later delivery when RabbitMQ is unavailable.

AI-analysis events use separate message contracts and routing from email
notification events.

Publisher support for AI-analysis events must be implemented explicitly;
the notification publisher must not be assumed to support arbitrary events.

## Analysis Boundary

A dedicated AI consumer host receives TicketAiAnalysisRequestedV1.

1. The consumer invokes AnalyzeRequestShell.
2. Shell retrieves analysis facts through repository abstractions.
3. Shell invokes ITicketAiAnalyzer.
4. OpenAiTicketAnalyzer in ServiceDesk.AI.OpenAI implements that interface.
5. Core validates the returned analysis and makes business decisions.
6. Shell persists analysis through a repository abstraction.

The consumer handles transport concerns.
The Shell handles use-case orchestration.
The OpenAI adapter handles provider HTTP communication and response parsing.

The OpenAI adapter does not consume RabbitMQ messages.

## Contracts and Names

- Backlog item: AiAnalysisRequest
- Shell analyzer interface: ITicketAiAnalyzer
- Analyzer operation: AnalyzeAsync
- Existing Core input: TicketAiAnalysisInput
- Existing Core result: TicketAiAnalysisResult
- Background use case: AnalyzeRequestShell
- OpenAI adapter: OpenAiTicketAnalyzer
- Persisted analysis: TicketAiAnalysis
- Integration event: TicketAiAnalysisRequestedV1
- Provider configuration: OpenAiOptions
- Provider registration: AddOpenAiAnalysis

ITicketAiAnalyzer returns the complete provider-independent result contract.
Core validates business requirements after the provider returns.

Provider failures must not be represented as successful results with
fabricated recommendation values.

## Dependency Direction

Core has no infrastructure dependencies.

Shell depends on Core and owns the abstractions required by its use cases.

ServiceDesk.AI.OpenAI implements the analyzer interface owned by Shell.
ServiceDesk.Repository implements persistence abstractions owned by Shell.
ServiceDesk.OutboxMessaging implements messaging infrastructure.

WebApi composes ticket-submission dependencies.
The dedicated AI consumer host composes background-analysis dependencies.

OpenAI configuration and AddOpenAiAnalysis belong to the AI consumer host.
The existing Outbox publisher does not require OpenAI configuration.

## Failure Independence

- OpenAI unavailable: the committed ticket remains submitted.
- OpenAI timeout: the committed ticket remains submitted.
- Invalid AI response: the committed ticket remains submitted.
- RabbitMQ unavailable: submission succeeds and the Outbox event is retained.
- AI consumer offline: submission succeeds and the durable message awaits processing.
- Ticket database transaction failure: submission fails.

Durable queue configuration and message acknowledgement behavior must be
verified when the consumer is implemented.

## Advisory Results

Analysis is persisted separately from the ticket.

AI recommendations must not automatically change ticket status, priority,
assignment, or history.

## Incremental Implementation

Contracts are established before implementing infrastructure.

Atomic submission integration, AI event publication, consumer orchestration,
analysis persistence, failure handling, retries, and concurrency protection
are introduced in separate tested increments.

Existing Core analysis contracts are retained.
Implementation ordering must respect dependencies between these increments.
