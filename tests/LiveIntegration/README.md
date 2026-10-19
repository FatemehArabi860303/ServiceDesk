# ServiceDesk live integration verification

## Current validation status

The Docker-backed deterministic run `sdverifye887a4dc7da84d23997115731faf445e`
passed all 13 required verification scenarios and cleanup on 2026-10-20. The optional real
OpenAI smoke test was **NOT RUN**. Restore and solution build passed; all **469 solution tests**
passed with none failed or skipped. Existing NuGet cryptography package vulnerability warnings
remain; the verifier also emits EF1002 for its isolated SQL CHECK constraint using a generated
GUID formatted with `D` (no user input).

Resumed verification confirmed and corrected case-insensitive Mailpit recipient matching,
bounded HTTP readiness timeout retries, and atomic report replacement when a Windows reader
briefly holds the destination. Focused regression tests cover these behaviors and the worker's
OpenAI request URL. Docker stop/start was found to reassign automatically published host ports;
the runner now explicitly binds distinct ports selected for each run, and verifies that broker
ports remain unchanged after restart.

## Prerequisites and commands

- Windows PowerShell **7.2+** (`pwsh`), .NET 10 SDK, and Docker Compose v2.
- A running Linux Docker engine capable of running the SQL Server 2022 Linux image (x64;
  allow sufficient memory for SQL Server, RabbitMQ and Mailpit).
- Permission to pull the images in `compose.yaml` and bind temporary loopback ports.
- No existing ServiceDesk database, broker, Mailpit, test accounts or OpenAI key is needed.

From the repository root:

```powershell
.\tests\LiveIntegration\Invoke-ServiceDeskVerification.ps1
.\tests\LiveIntegration\Invoke-ServiceDeskVerification.ps1 -CheckOnly
```

The runner has a 20-minute deadline, adjustable with `-TimeoutSeconds` (600–3600).
SDK/build commands and Docker startup/cleanup have separate bounded setup deadlines.
The broker-outage scenario intentionally honors the production **five-minute Outbox claim
lease**, so a successful run takes several minutes. Readiness and completion use bounded
polling against actual HTTP, SQL and broker evidence, rather than fixed startup delays.

Optional real provider verification (only when explicitly requested):

```powershell
# Set OpenAI__ApiKey through your secret-management environment; do not put it in this file.
$env:OpenAI__Model = '<a model available to your account>'
$env:OpenAI__BaseUrl = 'https://api.openai.com/'
.\tests\LiveIntegration\Invoke-ServiceDeskVerification.ps1 -UseRealOpenAI
```

The real mode runs the deterministic scenarios first, drains the AI queue, then submits one
synthetic ticket to the actual `OpenAiTicketAnalyzer`. A test-only observer rejects any second
provider invocation. There are no provider retries or automatic real-worker restarts. Keys/model
access/network failures are `BLOCKED`; malformed responses, Core validation or persistence
failures are `FAIL`. Without the flag, real OpenAI is `NOT RUN` and no real key is consumed.

The adapter sends the relative path `v1/responses`, so BaseUrl must be the API root shown
above. The worker default now uses that root. A focused regression test reads the actual worker
configuration and checks that the resolved request is `/v1/responses` using a stub HTTP handler;
it does not call the provider.

## What runs

The script creates a unique `sdverify<guid>` Docker Compose project containing SQL Server,
RabbitMQ management/AMQP, and Mailpit SMTP/API. It uses random loopback ports, a new database,
a dedicated RabbitMQ user/vhost, random credentials and test-only email addresses. No external
volumes, shared queues, developer database or existing Mailpit instance is used.
Selected loopback ports are explicitly bound so Docker retains them across stop/start. There
is a short interval between releasing the local port reservations and Docker binding them;
if another process takes a port, setup fails and the owned project is cleaned up.

The C# test executable:

1. Checks Docker project labels and mapped ports before opening the database or AMQP transport;
   applies actual EF migrations and starts the production WebApi executable on loopback.
2. Bootstraps an Administrator using the existing command. Creates, provisions, activates and
   logs in Customer/Employee accounts through the actual HTTP endpoints.
3. Submits a ticket while AI/publishing are offline. Checks one matching Outbox event. Uses a
   temporary SQL CHECK constraint to reject the next AI Outbox insert, then checks that the
   failed HTTP request left neither the ticket nor another Outbox event committed.
4. Runs the actual `OutboxPublisherWorker`, adapter and reusable publisher. Fetches/requeues
   the persistent AI envelope to check exchange, routing key, event ID and original payload.
5. Starts the actual `AiAnalysisConsumerWorker` through `AddAiWorker`, replacing only
   `ITicketAiAnalyzer` through test-host DI. Checks the real consumer, Shells, Core validation,
   SQL result, unchanged ticket state/history, and broker acknowledgement counter together.
6. Republishes the same event and checks one unchanged result, no additional analyzer call,
   and another acknowledgement. Repeats completed-event delivery after a worker restart.
7. Assigns a ticket via the real Employee workflow. Runs the actual notification worker,
   handler and SMTP adapter. Inspects Mailpit's API for the expected recipient, subject and
   body, and checks the published Outbox event plus broker acknowledgement.
8. Simulates analyzer failure and an actual SQL analysis-insert CHECK failure. Checks fail-stop,
   no completion/acknowledgement, retained delivery, and success after repair/restart.
9. Stops only the owned RabbitMQ container. Checks HTTP ticket success, an observed publisher
   failure and an unpublished Outbox claim. Restarts RabbitMQ, waits for normal lease recovery,
   and checks publication, durable analysis and acknowledgement.
10. Holds the deterministic analyzer behind a cancellation-aware signal. Checks unacknowledged
    active work, graceful cancellation, retained delivery and successful restart. This does
    **not** claim a forced OS crash, concurrent exactly-once execution, or atomic DB+BasicAck.

Worker hosts run in the test executable with production worker types and normal DI/scopes.
No production assembly references the fake or exposes a configuration switch to enable it.
Notification and Outbox composition is repeated in the test host because those executables
currently have top-level-only composition roots. Future registration changes must be reflected
here. The AI composition directly uses the existing `AddAiWorker` extension.

## Evidence, exit codes and cleanup

Reports are written after each scenario under the Git-ignored
`test-results/LiveIntegration/<run-id>/report.json`, with a sanitized final `verification.log`.
They contain status, sanitized stage
diagnostics, and synthetic event/ticket IDs. They never include credentials, JWT/activation
tokens, provider responses or raw request bodies. Application stdout/stderr is drained and
discarded to prevent accidental secret capture. This limits raw-error diagnostics deliberately.

- `PASS`: executed checks passed; `FAIL`: an executed check failed.
- `BLOCKED`: a required capability/provider configuration is unavailable.
- `NOT RUN`: not attempted; `SKIPPED`: intentionally omitted (for example cleanup when nothing started).
- `RUNNING` appears only in intermediate reports; an interrupted runner is finalized as `FAIL`.
- Exit 0: required checks passed (or explicit prerequisite-only mode).
- Exit 1: verification/setup/cleanup failed. Exit 2: prerequisite or requested provider mode blocked.

`finally` stops/disposes owned worker hosts, terminates only the WebApi process it started,
and runs `docker compose --project-name <generated-run-id> ... down --volumes`. It never stops
unrelated containers, drops a shared database, purges shared queues or deletes existing email.
If cleanup fails, the report identifies the owned project for manual removal. Host cleanup is
bounded; a timed-out handler fails the run instead of pretending graceful shutdown succeeded.
An OS/power failure can bypass finally; use the reported unique project name for manual cleanup.

Docker image tags currently follow maintained major releases/latest; exact image IDs can vary
between machines. Pin approved image digests in `compose.yaml` for a reproducible CI baseline
after the first Docker-backed run has been validated. No image availability is assumed here.
