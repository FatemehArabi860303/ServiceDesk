# Solution organization refactor (#99)

Original branch: `99-ai-assisted-customer-request-analysis`.
Original HEAD: `51acff29f8ef81cac8f628dc69c1bcf1f32a2ef0`.

## Production projects before

```text
src/
  Notification.Contracts/
  NotificationService.Email/
  NotificationService.Messaging/
  NotificationService.Shell/
  NotificationService.Worker/
  RabbitMq.Consumers/
  RabbitMq.Publishers/
  ServiceDesk.AI/
  ServiceDesk.AiWorker/
  ServiceDesk.Contracts/
  ServiceDesk.Core/
  ServiceDesk.Messaging/
  ServiceDesk.NotificationWorker/
  ServiceDesk.Repository/
  ServiceDesk.Shell/
  ServiceDesk.WebApi/
```

## Production projects after

```text
src/
  AI/
    ServiceDesk.AI.OpenAI/
    ServiceDesk.AI.Worker/
  Messaging/
    RabbitMq.Consumers/
    RabbitMq.Publishers/
  Notifications/
    Notification.Contracts/
    NotificationService.Application/
    NotificationService.Email/
    NotificationService.Messaging/
    NotificationService.Worker/
  ServiceDesk/
    ServiceDesk.Contracts/
    ServiceDesk.Core/
    ServiceDesk.Outbox.Worker/
    ServiceDesk.OutboxMessaging/
    ServiceDesk.Repository/
    ServiceDesk.Shell/
    ServiceDesk.WebApi/
```

## Test projects before

```text
tests/
  LiveIntegration.Tests/
  LiveIntegration/
  NotificationService.Messaging.Tests/
  NotificationService.Shell.Tests/
  RabbitMq.Consumers.Tests/
  RabbitMq.Publishers.Tests/
  ServiceDesk.AI.Tests/
  ServiceDesk.Core.Tests/
  ServiceDesk.Messaging.Tests/
  ServiceDesk.Repository.IntegrationTests/
  ServiceDesk.Shell.Tests/
  ServiceDesk.WebApi.IntegrationTests/
  TestSupport/
```

## Test projects after

```text
tests/
  AI/
    ServiceDesk.AI.OpenAI.Tests/
  LiveIntegration/
    ServiceDesk.LiveIntegration/
    ServiceDesk.LiveIntegration.Tests/
  Messaging/
    RabbitMq.Consumers.Tests/
    RabbitMq.Publishers.Tests/
  Notifications/
    NotificationService.Application.Tests/
    NotificationService.Messaging.Tests/
  ServiceDesk/
    ServiceDesk.Core.Tests/
    ServiceDesk.OutboxMessaging.Tests/
    ServiceDesk.Repository.IntegrationTests/
    ServiceDesk.Shell.Tests/
    ServiceDesk.WebApi.IntegrationTests/
  TestSupport/ (existing linked ConsumerSession fixture)
```

## Naming and compatibility

- ServiceDesk.Messaging becomes ServiceDesk.OutboxMessaging, ServiceDesk.AI becomes ServiceDesk.AI.OpenAI, ServiceDesk.AiWorker becomes ServiceDesk.AI.Worker, and NotificationService.Shell becomes NotificationService.Application. Their corresponding existing test projects follow the same names.
- ServiceDesk.NotificationWorker actually publishes both notification and AI Outbox events. NotificationService.Worker already hosts notification processing. The publisher host therefore becomes ServiceDesk.Outbox.Worker under ServiceDesk ownership.
- Notification.Contracts and NotificationService.Email remain intact under Notifications. No projects were invented or removed.
- Existing assembly names, root namespaces, C# namespaces, public types, configuration keys, and serialized contract identities are preserved. Renamed project files explicitly pin AssemblyName and RootNamespace to the previous defaults. Legacy namespaces and executable DLL names are intentional compatibility identities.
- LiveIntegration contains sibling runner and safety-test projects, plus its original script and Compose file. This prevents SDK source globs from including one project in the other.
- ServiceDesk.OutboxMessaging.Tests retains its existing related AI worker, transport and notification coverage. ServiceDesk.Shell.Tests retains its existing repository-backed tests. Test projects were not split.
- TestSupport/ConsumerSession.cs remains the existing linked fixture used by multiple component suites; its relative links are updated.

## References and boundaries

All 28 projects occur once in the solution. All project and linked-fixture references resolve; the dependency graph has no cycles. Both RabbitMQ libraries retain application-independent dependencies. The Core dependency boundary remains unchanged.

Existing naming/boundary limitation: OutboxMessaging also owns TicketAiAnalysisMessageHandler, an inbound transport adapter. That responsibility and the combined tests are retained; separating it would be a distinct redesign. Notification validation and orchestration stay together in Application; no artificial Functional Core is introduced.

Updated the solution groups, project paths, linked AI worker configuration, local run/bootstrap EF and WebApi commands, AI worker README, AI architecture documentation, live runner build/DLL path and WebApi process discovery. CI builds the root solution and requires no path changes. Compose contains only infrastructure services and no relocated build contexts. EF migrations and assembly scanning are unchanged. Ignore rules are path-independent.

The only changed C# content is the live verifier WebApi executable path. All production C# and test assertions remain unchanged. The pre-existing RabbitMqConsumer.cs line-ending difference is preserved byte-for-byte at its relocated path and its Git-normalized content still matches HEAD. No unrelated content change has been staged.

## Validation

Resumed verification on 2026-10-20:

- Command execution: PASS; Windows error 740 did not recur. Git fsck found no corruption (only dangling objects).
- Restore: PASS with the normal .NET CLI environment.
- Build: PASS, 0 errors and 32 warnings. Existing NuGet cryptography package vulnerability warnings and verifier EF1002 remain; no dependencies were changed.
- Solution tests: PASS, 469 passed, 0 failed, 0 skipped across all 11 test assemblies. The twelfth test-area project is the live verification console runner. TRX totals were independently checked.
- EF migration discovery: PASS, all nine migration identities listed using --no-connect from the relocated Repository project.
- Live deterministic integration: PASS, exit 0. Run `sdverify637e02cc880049f0b6418bb99227960d` passed all 13 required scenarios, including broker recovery after the normal five-minute Outbox lease and graceful worker shutdown/restart. Real OpenAI was NOT RUN in this structural verification.
- Cleanup: PASS. No containers, volumes or networks with this run's Compose project label remain. All pre-existing containers were preserved.
- Static review: all 288 deleted old files have one relocated counterpart; all 28 projects (16 production, 11 test assemblies and one live runner) occur exactly once in the solution. All project/content/fixture references resolve, with no dependency cycles. Old project directories contain no remaining source or test projects.
- Production C# and test assertions match HEAD after line-ending normalization. The only changed C# content is the verifier's relocated WebApi executable path. RabbitMqConsumer.cs retains its pre-existing exact bytes (SHA-256 502dc81a627d1879c42cf3df30cbe4251e69161827312593ebf670ad3cc63fb3).
- All legitimate relocated test files under AI, Messaging, Notifications, ServiceDesk and LiveIntegration are included. Package versions, target frameworks, public identities and host appsettings are preserved.
- Full staged diff reviewed; git diff --cached --check passed. Local commit is authorized; no push or PR is requested in this verification session.

The earlier blocked Docker and Git index attempts were environment limitations of the prior session and are superseded by this verification. Old build outputs remain preserved under ignored artifacts/structure-refactor/previous-build-output.

Detailed resumed build, test, original status and diff evidence is under ignored artifacts/structure-refactor/. The sanitized live report is under test-results/LiveIntegration/sdverify637e02cc880049f0b6418bb99227960d/. Feature #99 acceptance and its separately authorized real-provider smoke test follow the structural commit; this refactor does not claim full feature acceptance.
