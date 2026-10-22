# Start Work on a Request — Employee Web UI (#105)

PBI: https://github.com/FatemehArabi860303/ServiceDesk/issues/105. Parent: #4 Deliver Support. Work is local on `feature/105-employee-start-work-ui`; push/PR/merge/closure require explicit approval.

## Backend inspection and scope

Inspected latest main `a7b184e`, including merged customer UI #103 and backend #99. #100 View AI Analysis and #101 AI Privacy and Observability are still open. No existing employee UI PBI duplicates #105. Existing lifecycle PBIs #16/#20 and list PBI #85 are completed backend work.

| Capability | Existing implementation and tests | Integration added for #105 |
| --- | --- | --- |
| Employee login | `WebApi/Controllers/AuthenticationController.cs`, POST `/api/auth/login`; `Shell/Authentication/AuthenticateUserShell.cs`; AuthenticateUser Core/Shell/API tests | Existing JWT/localStorage auth reused; employee landing/protected routes added |
| List across customers | GET `/api/tickets`; `GetAllTicketsShell`, `TicketRepository.GetAllAsync`; GetAllTickets Shell/API tests | Existing endpoint has no paging/filtering. Add a separate bounded employee read; leave original contract unchanged |
| Ticket details | `ITicketRepository.GetByIdAsync` includes history; no HTTP detail read | Employee detail endpoint and dedicated read models, without expanding customer DTOs |
| Saved AI | `Repository/AiAnalysis/TicketAiAnalysisRepository.cs` saves independent validated results; Shell/persistence/worker tests | No read API exists. Remains #100's responsibility; this UI cannot display saved recommendations yet |
| Assign | POST `/api/tickets/{id}/assignment`; `AssignTicketShell`, `AssignTicketCore`, `Ticket.Assign`; existing Core/Shell/repository/API tests | Domain rules reused in combined operation |
| Start work | POST `/api/tickets/{id}/start-work`; `StartWorkShell`, `StartWorkCore`, `Ticket.StartWork`; existing Core/Shell/repository/API tests | Original API requires prior assignment. Add atomic claim-and-start, not a browser sequence |
| Concurrency | Conditional SQL update on Open/status and expected assignee; transaction includes history/Outbox; no row-version/ETag | One conditional assignment+status update, transactionally writing both history actions and corresponding notifications |

Production projects live under `src/ServiceDesk/`; relevant backend tests under `tests/ServiceDesk/`; React under `frontend/`. New orchestration is `EmployeeWorkShell`, pure decisions in `EmployeeWorkCore`, persistence through `IEmployeeWorkRepository` implemented by existing `TicketRepository`, and HTTP mapping in `EmployeeTicketsController`.

### Actual new API contract

All three endpoints require an Employee JWT **and a persisted active Employee**. Identity comes from the authenticated subject, never a request-supplied employee ID. Customers/administrators cannot use this employee workspace API; original administrator list access remains unchanged.

- GET `/api/employee/tickets?page=1&pageSize=20`: `{ items, page, pageSize, hasMore }`. Default 20, maximum 50 rows; invalid page/size → 400. Stable ordering by ticket GUID, with one bounded lookahead row to determine `hasMore`. There is no pre-existing search/filter/pagination to reuse. Paging is not a snapshot; use Refresh to see concurrent changes.
- Each item: `id`, `title`, `customerEmail`, `assignedEmployeeUserId`, `assignedEmployeeEmail`, numeric `priority`/`status`, `createdAt`, `updatedAt`.
- GET `/api/employee/tickets/{id}`: `{ ticket, description, history, canStartWork }`. `ticket` is the item above; history contains `id`, `ticketId`, `actorUserId`, numeric `action`, `occurredAt`, nullable `assignedEmployeeUserId`. Missing ticket → 404. Eligibility is decided by Core from server facts.
- POST `/api/employee/tickets/{id}/start-work`: body `{}`; success 204. Active Employee may claim an unassigned Open ticket or start their own assigned Open ticket. Other assignment/unsupported state/race → 409; missing → 404; invalid/expired auth → 401; forbidden/inactive persisted identity → 403.
- A successful unassigned claim records Assigned then WorkStarted, sets assignment and InProgress, and atomically commits two progress-notification Outbox events. Starting an existing own assignment records only WorkStarted and its notification. No schema migration or provider call is needed. Existing assigned-only start API retains its semantics.
- Current concurrency rules are state/assignee conditional writes, not client-supplied version tokens. A losing write commits no new history or Outbox event. There is no automatic retry that might claim another employee's work.

### AI dependency and truthful display

#100 is unimplemented: no authorized saved-analysis read API exists. The detail page says recommendations are not available in this workspace and that this does not establish whether an analysis has been saved. It never claims pending/failed/completed analysis or displays fabricated results. It cannot yet satisfy available-analysis rendering or its tests; those remain explicitly blocked by #100. #100/#101 are not implemented or closed by this PBI. No AI request is made and no authoritative ticket field is changed by AI.

## Start the existing Windows environment

Prerequisites: PowerShell 7, .NET 10 SDK, EF Core 10 tooling, SQL Server Express LocalDB, Node/npm and a browser. Detailed startup/troubleshooting is in [the verified customer guide](Submit-Request-UI-Manual-Test.md). Docker, RabbitMQ, Mailpit, AI, notification and Outbox workers are unnecessary for this test.

Terminal A:

```powershell
Set-Location 'C:\Users\ftmar\Documents\Codex\2026-09-18\we-are-going-to-build-a\ServiceDesk'
dotnet --version
dotnet ef --version
sqllocaldb info MSSQLLocalDB
& .\scripts\run-local.ps1
```

The wrapper migrates `ServiceDeskLocal`, configures its integrated-security LocalDB connection and temporary JWT signing key, then runs `src/ServiceDesk/ServiceDesk.WebApi` with the `http` profile on `http://localhost:5011`. Restarting with a different generated key invalidates old tokens.

Terminal B:

```powershell
Set-Location 'C:\Users\ftmar\Documents\Codex\2026-09-18\we-are-going-to-build-a\ServiceDesk\frontend'
npm ci
npm run dev -- --host localhost --strictPort
```

Vite is on `http://localhost:5173`. Existing local `.env` points `VITE_API_BASE_URL` at 5011; an unset base uses the existing `/api` proxy to 5011. Do not put secrets in VITE variables. Configured CORS origin is `http://localhost:5173`; use that exact origin. Keep both terminals running.

Terminal C:

```powershell
(Invoke-WebRequest 'http://localhost:5011/api/employee/tickets' -SkipHttpErrorCheck).StatusCode
Start-Process 'http://localhost:5173/login'
```

Anonymous probe should return 401. The API has no Swagger/health endpoint; `/` may return 404 normally.

## Employee development account

Prefer an existing activated Employee whose password you know; none is hardcoded or seeded. If needed, use a known development Administrator at `/admin/users`, **Add User**, select **Employee** (numeric role 1), create a unique test email, then **Provision Access** for that user. Activate through POST `/api/auth/activate` using the secure prompt-based PowerShell example in the customer guide. Password policy is 15–128 Unicode code points. The activation procedure is the same for Employees and Customers. Do not print passwords, activation tokens, JWTs or unredacted HAR files.

Only on an empty installation without an Administrator, stop terminal A, run `scripts/bootstrap-local.ps1` from the root and follow its secure prompts; then restart the API. Bootstrap is not a password-reset procedure and must not be used to replace an existing installation. A second activated Employee is useful for conflict checks. Use synthetic development data, not another user's live work.

## Manual scenario and reusable checklist

- [ ] As a Customer, submit a synthetic ticket using the existing customer UI: title **Employee UI Test Request**, description **Testing atomic start work through the employee workspace.**, Medium priority. Record the returned ID. Sign out. Existing eligible synthetic tickets can be reused instead.
- [ ] Sign in at `/login` with the active Employee. POST `/api/auth/login` returns 200. Expect `/employee`, **Support requests**, loading then ticket rows across customers. Each row shows title/ID/customer/status/priority/assignment/creation date.
- [ ] Check Network: only GET `/api/employee/tickets?page=1&pageSize=20`, never an unbounded GET `/api/tickets`. Check Previous/Next on a database with more than 20 tickets; no need to create bulk data merely to complete a smoke test. Refresh requests performs a new bounded fetch.
- [ ] Open the recorded ticket. Expect `/employee/tickets/{id}` with title, description, customer, timestamps, priority/status, assignment and Request history. Created action is present. Confirm the neutral AI-unavailable message; available saved AI rendering remains blocked on #100 even if SQL already contains analysis.
- [ ] On an Open/unassigned ticket, **Start Work** is enabled from server eligibility. Click once. The button disables and reads **Starting work…**. No separate `/assignment` call is made. No success appears before the combined POST returns 204.
- [ ] After confirmation, a fresh detail GET shows **In progress**, the Employee's email as assignment, and Created → Assigned → Work started history. Existing own-assigned tickets instead add only Work started. Record the ID/status, without secrets.
- [ ] **Back to requests** fetches a fresh page and shows updated status/assignment. For an independently refreshed check, reload the detail page: it must still show persisted state/history. There is no need to inspect SQL manually.
- [ ] Start Work is disabled after starting, for another Employee's assignment, and for InProgress/Resolved/Closed states. Core/server remains authoritative even if browser state is stale.
- [ ] Two-employee conflict: open the same eligible ticket as separate Employees in separate browser profiles (not tabs sharing localStorage). Let Employee A start it. In Employee B's stale page click Start Work. Expect 409, a friendly conflict message and refreshed state showing A's assignment. No extra assignment/history should appear. If B refreshes first, the button is already disabled.
- [ ] Anonymous/private-window `/employee` or direct detail URL redirects to `/login` without data. Customer access cannot expose employee pages; customer Bearer calls to the workspace API return 403. Administrator still uses administrator routes and cannot start employee work here.
- [ ] Restart-generated/expired session: expect 401, session cleared, login prompt; sign in again. Persisted inactive or role-changed Employee is forbidden even if an older JWT still claims Employee.
- [ ] Missing ticket GUID: 404 and friendly not-found state. No Start Work control. Server error/network failure: safe message, no internal diagnostics. Reload request is available when data could not be read. After an uncertain mutation response, refresh before retrying.
- [ ] Test keyboard navigation, focus visibility, table scrolling/mobile layout, labels, loading, empty page and error/retry states in an actual browser. These visual checks are distinct from JSDOM integration.
- [ ] Sign out, Ctrl+C in frontend then backend terminals. Stop LocalDB only if started solely for this test and unused elsewhere (`sqllocaldb stop MSSQLLocalDB`, without force). Do not delete shared data or stop unrelated processes/containers. Test tickets intentionally persist; no delete endpoint is implemented.

## Automated checks

From the repository root:

```powershell
dotnet build ServiceDesk.slnx
dotnet test ServiceDesk.slnx --no-build
```

From `frontend`:

```powershell
npm run typecheck
npm run build -- --outDir ../artifacts/employee-ui/dist
npm test -- --run --maxWorkers=1 --minWorkers=1
```

Build output is directed to ignored artifacts because existing `frontend/dist` files are tracked. Do not commit generated output. No linter is configured. The full frontend suite has a pre-existing UsersPage provisioning-message assertion failure (exact message vs rendered `Error:` prefix); keep it separate from employee acceptance.

Opt-in real-HTTP check: `npm run test:employee:live -- --maxWorkers=1 --minWorkers=1`. Supply process-only `VITE_API_BASE_URL`, `SERVICEDESK_TEST_EMPLOYEE_EMAIL`, `SERVICEDESK_TEST_EMPLOYEE_PASSWORD`, and `SERVICEDESK_TEST_TICKET_ID` through secure local environment input. It mutates a ticket titled **Employee UI Test Request** on the first page; target only an isolated development database/owned synthetic ticket. It renders real React in JSDOM and calls the real API, without auth or submission mocks. It does not provide browser visual/CORS verification. Do not put credentials in files, history, source or screenshots.

## Verification evidence and limitations

Final automated results: solution build PASS (0 errors, 32 existing dependency/verifier warnings); full backend suite **503 passed, 0 failed/skipped**. Frontend typecheck and production build PASS. Full frontend suite **48 passed, 1 failed** at the unchanged administrator `UsersPage.test.tsx:105` provisioning-message assertion; all **18 employee-flow tests** and **20 customer-flow tests** passed. The separate real-HTTP rendered UI acceptance test passed once. Guide PowerShell blocks parsed without errors. No lint tool is configured.

On 2026-10-22 an isolated LocalDB database was migrated; a disposable administrator was bootstrapped; a Customer and two Employees were created, provisioned and activated through existing HTTP endpoints. The Customer created a synthetic ticket. The opt-in rendered React/real-HTTP test passed employee login, list, details, atomic start, refreshed details and list. Independent SQL readback confirmed InProgress, assignment to the authenticated Employee, and Assigned/WorkStarted actors. Another Employee's attempt returned 409; Customer list access returned 403. No workers or provider calls ran. The owned API process and isolated database were removed; shared development data was preserved.

Backend regression coverage includes persisted active-role checks, forged identity, paging bounds/cross-customer reads, history/details, missing tickets, lifecycle rejection, own/unassigned starts, stale conditional writes and rollback of assignment/status/history/Outbox on persistence failure. Frontend coverage includes employee login/protection, bounded pages, detail/history, neutral AI state, eligibility, duplicate prevention, refresh, authorization/conflicts, network/server errors and customer regression.

An interactive browser was unavailable (empty browser inventory and failed in-app-browser creation). Manual visual/mobile/CORS end-to-end acceptance was not executed. AI available/pending-result rendering remains incomplete by design pending #100's authorized read API; neither absence nor the workspace limitation is presented as proof of processing state. The PBI remains In Progress, not Done.
