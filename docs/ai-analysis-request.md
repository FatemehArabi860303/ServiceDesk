# AI-Assisted Customer Request Analysis

## Architecture Decision

AI analysis is asynchronous and must not block customer ticket submission.

### Submission Workflow

1. CreateTicketShell validates the customer and invokes CreateTicketCore.
2. Core creates the ticket using existing business rules.
3. Shell requests persistence of the ticket and its AI-analysis intent.
4. Repository saves the ticket, pending analysis record, and outbox event atomically.
5. Shell returns the created ticket to the customer.

### Analysis Workflow

1. Outbox publisher delivers the analysis-requested event.
2. AI worker receives the event and invokes AnalyzeRequestShell.
3. Shell retrieves the analysis context through repository abstractions.
4. Shell invokes ITicketAiAnalyzer.
5. OpenAI adapter communicates with the external AI service.
6. Core validates the result and determines the next analysis state.
7. Shell persists the resulting state.

### ISFC Rules

- Core must be deterministic and free of I/O.
- Shell coordinates operations and invokes pure Core functions.
- Repository performs persistence.
- Infrastructure adapters perform external communication.
- AI responses are untrusted until validated by Core.

### DIP Rules

- Shell owns ITicketAiAnalyzer.
- OpenAI adapter implements ITicketAiAnalyzer.
- Shell must not reference OpenAI-specific types.
- Dependency injection supplies concrete implementations.

### Failure Isolation

Ticket submission does not invoke OpenAI.

Provider failures do not invalidate saved tickets.

Transient failures are retried with bounded attempts.

Permanent failures are recorded without modifying ticket status.

### Consistency

Ticket creation, pending analysis creation, and outbox insertion must succeed or fail atomically.

Duplicate event delivery must not generate duplicate completed analyses.
