# Submit a Request — Customer Web UI manual test

Verified against local commit `d60af3331409fd608b3280b758aeff47e394a014` on 2026-10-22. This guide uses the repository's Windows development configuration. It does not require AI, notification or Outbox workers.

## Prerequisites and actual layout

- Windows, PowerShell 7, .NET 10 SDK, SQL Server Express LocalDB (`sqllocaldb.exe`), and `dotnet-ef` compatible with EF Core 10.
- Node.js and npm. Verification used .NET 10.0.301, dotnet-ef 10.0.0, Node 24.22.0 and npm 11.19.1; these are observed versions, not a declared Node engine requirement.
- An active development Customer account whose password you know. An existing Administrator account is needed only to provision a customer or independently retrieve saved tickets. There are no seeded/default credentials in the inspected source. Passwords cannot be recovered from stored hashes.
- Free localhost ports 5011 and 5173. Use a development database: the setup script applies migrations, and submitting a ticket persists data.

The solution is `ServiceDesk.slnx`. The application projects are under `src/ServiceDesk/`, with AI, Messaging and Notifications in separate sibling groups. Relevant backend tests are under `tests/ServiceDesk/`; the infrastructure verifier is under `tests/LiveIntegration/`. React/TypeScript is in `frontend/`, with its own `package.json`.

Set the working directory in each new PowerShell terminal:

```powershell
Set-Location 'C:\Users\ftmar\Documents\Codex\2026-09-18\we-are-going-to-build-a\ServiceDesk'
dotnet --version
dotnet ef --version
node --version
npm --version
sqllocaldb info MSSQLLocalDB
Get-NetTCPConnection -State Listen -LocalPort 5011,5173 -ErrorAction SilentlyContinue |
    Select-Object LocalAddress,LocalPort,OwningProcess
```

If EF tooling is missing, the compatible version observed here can be installed with `dotnet tool install --global dotnet-ef --version 10.0.0`. Do not replace an existing tool installation without checking its version. Install missing LocalDB through SQL Server Express/Visual Studio tooling before proceeding.

## 1. Infrastructure: LocalDB, not Docker

**No Docker startup command is required for this test.** `scripts/run-local.ps1` explicitly selects:

```text
Server=(localdb)\MSSQLLocalDB;Database=ServiceDeskLocal;Trusted_Connection=True;MultipleActiveResultSets=True
```

It overrides any process `ConnectionStrings__ServiceDesk` while running, applies migrations, and restores the previous environment afterward. LocalDB starts on connection. Do not run this script expecting it to target a Docker SQL database.

The actual Compose file is `tests/LiveIntegration/compose.yaml`, containing `sql`, `rabbitmq` and `mailpit`. It belongs to the isolated automated verifier and requires `SDV_SQL_PASSWORD`, `SDV_SQL_PORT`, `SDV_BROKER_PASSWORD`, `SDV_RUN_ID`, `SDV_AMQP_PORT`, `SDV_MANAGEMENT_PORT`, `SDV_SMTP_PORT` and `SDV_MAILPIT_PORT`. It is not the Windows development startup configuration. Do not run that entire verifier or invent Compose environment values for this UI smoke test. Leave existing containers untouched.

## 2. Start the backend (terminal A)

From the repository root:

```powershell
& .\scripts\run-local.ps1
```

Leave this terminal running. Expected: migrations complete and `Now listening on: http://localhost:5011`.

The script runs these actual project paths after setting the connection string:

```powershell
dotnet ef database update --project .\src\ServiceDesk\ServiceDesk.Repository --startup-project .\src\ServiceDesk\ServiceDesk.Repository --connection 'Server=(localdb)\MSSQLLocalDB;Database=ServiceDeskLocal;Trusted_Connection=True;MultipleActiveResultSets=True'
dotnet run --project .\src\ServiceDesk\ServiceDesk.WebApi --launch-profile http
```

Use the wrapper as the executable startup procedure: the two commands alone do not set all required runtime configuration. The `http` launch profile sets `ASPNETCORE_ENVIRONMENT=Development` and port 5011. `appsettings.json` supplies JWT issuer `ServiceDesk`, audience `ServiceDesk.Client`, and CORS origin `http://localhost:5173`. It contains no connection string or signing secret. The wrapper generates a temporary `Jwt__SigningKey` if one is absent (at least 32 UTF-8 bytes are required). No manual secret creation is needed. Restarting with a new key invalidates old browser tokens; sign in again.

There is no configured Swagger or health endpoint. A 404 at `/` does not mean startup failed. This read-only probe should return **401**, proving the protected controller is reachable:

```powershell
(Invoke-WebRequest 'http://localhost:5011/api/tickets' -SkipHttpErrorCheck).StatusCode
```

## 3. Start React (terminal B)

```powershell
Set-Location 'C:\Users\ftmar\Documents\Codex\2026-09-18\we-are-going-to-build-a\ServiceDesk\frontend'
npm ci
npm run dev -- --host localhost --strictPort
```

`npm ci` installs the checked-in lockfile dependencies; it is unnecessary on every run when already installed. Expected: `http://localhost:5173/`. `--strictPort` prevents silently moving to another port.

`src/api/apiClient.ts` uses `VITE_API_BASE_URL` or an empty base. The existing local `frontend/.env` was inspected safely and currently sets `VITE_API_BASE_URL=http://localhost:5011`. That uses the configured CORS allowlist. With no base configured, Vite's existing `/api` proxy also targets `http://localhost:5011`. Both paths are supported. A process environment value overrides `.env`; inspect/correct any stale `VITE_API_BASE_URL` before launching. Never put credentials in `VITE_` variables: they are public bundle configuration. Restart Vite after environment changes.

From terminal C:

```powershell
(Invoke-WebRequest 'http://localhost:5173/login').StatusCode
Start-Process 'http://localhost:5173/login'
```

Expected HTTP 200 and a ServiceDesk sign-in page. The React app is served by Vite, not by the API.

## 4. Obtain valid customer access

### Prefer an existing customer

Use the email/password of an already activated development Customer. No particular email/password is assumed. If unavailable, ask the development account owner. The local database had seven users during verification, but no known customer password was supplied. Do not bootstrap another administrator merely because you do not know the existing password.

### Provision only when a new development customer is needed

Use the existing supported flow below on your own development installation. It creates durable account data. You need a known Administrator login. Do not reuse an existing email or reprovision an activated account (409 conflict).

1. Sign in as Administrator at `/login`, then open `http://localhost:5173/admin/users`.
2. Select **Add User**, enter development first/last name and a unique email, explicitly select **Customer** (the form defaults to Employee), then **Create**.
3. On the Users page select **Provision Access** for that customer. Keep the returned activation token private and note its expiry. There is no implemented customer activation web page.
4. Activate through the existing API using terminal C. Prompt input keeps secrets out of command history; do not run under a transcript or print/export the token or request body:

```powershell
$activationSecret = Read-Host 'Activation token from Provision Access' -AsSecureString
$passwordSecret = Read-Host 'Choose customer password (15–128 Unicode code points)' -AsSecureString
$activationCredential = [pscredential]::new('activation', $activationSecret)
$passwordCredential = [pscredential]::new('customer', $passwordSecret)
$activationBody = @{
    activationToken = $activationCredential.GetNetworkCredential().Password
    password = $passwordCredential.GetNetworkCredential().Password
} | ConvertTo-Json
try {
    $activationResponse = Invoke-WebRequest 'http://localhost:5011/api/auth/activate' -Method Post -ContentType 'application/json' -Body $activationBody -SkipHttpErrorCheck
    "Activation HTTP status: $($activationResponse.StatusCode)"
} finally {
    Remove-Variable activationBody,activationSecret,passwordSecret,activationCredential,passwordCredential,activationResponse -ErrorAction SilentlyContinue
}
```

Expected **204 No Content**. On failure, do not dump credential-bearing request data; check expiry, password length and whether the token was already used. Password plaintext exists briefly in process memory for the HTTP request; close the terminal afterward.

If and only if this is a genuinely empty installation without an administrator, stop terminal A, run `& .\scripts\bootstrap-local.ps1` from the root, and answer its secure prompts. It migrates `ServiceDeskLocal` and invokes `--bootstrap-administrator`; bootstrap is not a password-reset mechanism. Restart `run-local.ps1`, then follow the administrator flow above. The API has no automatic seed account. Existing-installation bootstrap rejection must not be worked around by deleting the database.

## 5. Manual customer scenario

1. Sign out of Administrator if used. At `http://localhost:5173/login`, enter the activated Customer's email/password and select **Sign in**.
2. Login calls `POST /api/auth/login`; success is **200 OK** with `accessToken` and `expiresAt`. The browser stores the session under localStorage key `sd_auth`. Keep tokens private.
3. Expect `/customer`, heading **How can we help?**, and a prominent **Submit a Request** action.
4. Select it. Expect `/customer/requests/new`, heading **Submit a Request**, Title, Description, Priority, Submit request and Cancel.
5. Enter exactly:

   | Field | Value |
   | --- | --- |
   | Title | `Customer UI Test Request` |
   | Description | `Testing ticket creation through the ServiceDesk customer frontend.` |
   | Priority | Medium |

6. Open browser Developer Tools → Network, then select **Submit request** once. Fields/button disable while processing and the button reads **Submitting…**. Repeated clicks while processing must not create multiple requests.
7. Inspect the ticket request, without exporting an unredacted HAR (it includes credentials/tokens). Method is `POST`, path `/api/tickets`, Bearer authentication. With current `.env`, the URL is `http://localhost:5011/api/tickets`; proxy mode displays `http://localhost:5173/api/tickets`.

```json
{
  "title": "Customer UI Test Request",
  "description": "Testing ticket creation through the ServiceDesk customer frontend.",
  "priority": 1
}
```

The request contains no customer ID. Priority is numeric: Low 0, Medium 1, High 2, Critical 3. Identity comes from the JWT subject and the server's active Customer check.

Expected response: **201 Created**, `Location: /api/tickets/{returned-id}`, and JSON fields `id`, `customerUserId`, `title`, `description`, `priority`, `status`, `createdAt`, `updatedAt`. Verify the exact title/description, priority `1`, status `0` (Open), a nonempty GUID `id`, and server timestamps. Do not expect a fabricated fixed ID.

8. Only after API confirmation, expect **Your request has been submitted**, a success message, and **Ticket ID** matching the response `id`. Record that synthetic ID. **Create another request** clears the form; **Return to customer page** returns to `/customer`.

Creation persists the ticket and its pending Outbox event before returning. It does not require RabbitMQ or an AI provider. No analysis result or notification is expected while workers are stopped.

## 6. Independently confirm the saved ticket

There is currently no customer ticket-history screen. The API supports **GET `/api/tickets` for Administrator or Employee only**. Customers receive 403 for that list. Although creation returns a Location header, no GET `/api/tickets/{id}` action is implemented; opening that URL is not a valid save check.

In terminal C, use a known development Administrator (or Employee) login and the ID from the confirmation page. This reads existing state without creating a second ticket:

```powershell
$verifyCredential = Get-Credential -Message 'Development Administrator or Employee email/password'
$verifyLoginBody = @{
    email = $verifyCredential.UserName
    password = $verifyCredential.GetNetworkCredential().Password
} | ConvertTo-Json
try {
    $verifyLogin = Invoke-RestMethod 'http://localhost:5011/api/auth/login' -Method Post -ContentType 'application/json' -Body $verifyLoginBody
    $ticketId = [guid](Read-Host 'Ticket ID shown by the customer confirmation')
    $savedTickets = Invoke-RestMethod 'http://localhost:5011/api/tickets' -Headers @{ Authorization = "Bearer $($verifyLogin.accessToken)" }
    $savedTicket = @($savedTickets | Where-Object { $_.id -eq $ticketId.ToString() })
    if ($savedTicket.Count -ne 1) { throw 'Expected exactly one matching saved ticket.' }
    if ($savedTicket[0].title -ne 'Customer UI Test Request' -or
        $savedTicket[0].description -ne 'Testing ticket creation through the ServiceDesk customer frontend.' -or
        $savedTicket[0].priority -ne 1 -or $savedTicket[0].status -ne 0) {
        throw 'Saved ticket fields do not match the manual scenario.'
    }
    $savedTicket | Select-Object id,title,description,priority,status,createdAt
} finally {
    Remove-Variable verifyCredential,verifyLoginBody,verifyLogin,savedTickets,savedTicket,ticketId -ErrorAction SilentlyContinue
}
```

Expected **200 OK** and one matching row. Do not display all tickets: the list may contain other development data. Without a privileged development login, this independent readback remains blocked; the UI confirmation and 201 are available evidence but do not substitute for this step.

## 7. Negative checks

- **Required fields:** Open a fresh form and submit empty. Expect `Enter a title for your request.`, `Describe what you need help with.`, and `Choose a priority.` Focus moves to Title. No ticket POST is sent. Whitespace-only title/description also fail.
- **Limits:** Title has native `maxlength=200`; description has `maxlength=5000`. Normal typing/pasting cannot exceed them. Test the maximum lengths on a separate synthetic request if desired; do not expect normal browser input to trigger an over-limit error because the native limit prevents excess input.
- **Cancel:** Enter values and select Cancel. Expect dashboard and no ticket POST.
- **Anonymous route:** Sign out, then directly open `http://localhost:5173/customer/requests/new` in a private window. Expect `/login`, no request fields, and no submission. HTTP 200 for the SPA URL alone does not prove authorization: React protects the route.
- **Wrong role:** Sign in as Administrator and navigate to the customer form. Expect no customer form (the login redirect returns administrators to `/admin`). Employee access likewise cannot expose the form.
- **Anonymous API:** The following sends the scenario body without authorization. Expect **401**, not 201, and no ticket created:

```powershell
$anonymousBody = @{
    title = 'Customer UI Test Request'
    description = 'Testing ticket creation through the ServiceDesk customer frontend.'
    priority = 1
} | ConvertTo-Json
(Invoke-WebRequest 'http://localhost:5011/api/tickets' -Method Post -ContentType 'application/json' -Body $anonymousBody -SkipHttpErrorCheck).StatusCode
Remove-Variable anonymousBody
```

- **Expired session:** After restarting the API with a newly generated signing key, an old session's ticket POST returns 401; the form clears authentication and navigates to login with `Your session has expired.` Sign in again. Restoring an already expired stored session also redirects to login.
- **Network error (optional):** Use DevTools Offline during submission on a fresh form. Expect a friendly connection message, retained input and no claimed success. Restore networking before retrying. A lost response can leave an uncertain outcome; check the saved list before sending again.

## 8. Common problems

| Symptom | Check/fix |
| --- | --- |
| Missing LocalDB / SQL connection failure | Run `sqllocaldb info MSSQLLocalDB`; install LocalDB if absent. `sqllocaldb start MSSQLLocalDB` can start the existing instance. Use the wrapper's actual database, not an unrelated Docker database. |
| Missing EF command | Install compatible dotnet-ef as described above; restart the terminal if PATH changed. |
| API asks for connection/signing configuration | Use `scripts/run-local.ps1`. Direct `dotnet run` does not generate these settings. |
| API port already in use | Inspect listener PID with the prerequisite command. Reuse the intended running app or stop it from its own terminal; do not kill arbitrary processes. |
| Vite reports port in use | Stop the earlier intended frontend, then retry. Do not accept an alternate port: CORS is configured for 5173. |
| Login rejected | Verify known credentials, active Customer role, provisioning and completed activation. No default password exists. |
| Bootstrap rejected | An installation already exists or data fails validation. Obtain existing administrator access; do not reset/delete data. |
| Provisioning returns 409 | Existing login credentials cannot be provisioned again; use the existing account or a new unique development customer. |
| Browser CORS/network error | Inspect actual Request URL and `VITE_API_BASE_URL`; use localhost consistently (not an assumed HTTPS or 127.0.0.1 origin), keep ports 5011/5173, restart Vite after configuration changes. |
| Root API / Swagger / ticket Location returns 404 | Those GET endpoints are not configured. Use the protected-list probe and supported list readback. |
| 400/403 on ticket submission | Check fields and priority; only an active persisted Customer can submit. Administrator/Employee tokens cannot create customer tickets. |
| NU1902/NU1903 warnings | Existing cryptography package warnings were observed; they did not prevent startup. Dependency remediation is outside this guide. |
| Frontend tests fail in UsersPage | A known existing provisioning-message assertion fails; it is separate from the manual customer flow. |

## 9. Shutdown

1. Sign out in the browser and close any private testing window. Close DevTools without exporting secrets.
2. Press **Ctrl+C** in terminal B (Vite), then terminal A (WebApi). Wait for shutdown. The backend wrapper restores its process connection configuration and removes a signing key it generated.
3. Leave LocalDB running if it was already running or another application uses it. If you started an otherwise unused instance solely for this test, after both apps stop you may use `sqllocaldb stop MSSQLLocalDB` (no force flag). Do not delete the instance/database; the saved ticket is intentional development data. There is no implemented ticket-delete endpoint for cleanup.
4. No Docker services were started by these instructions. Do not run a broad Compose down or stop unrelated containers/workers.
5. Check listener ownership if desired using the prerequisite command. If a process remains, stop only the process you started and have positively identified; avoid blanket `Stop-Process -Name dotnet,node`.

## Validation performed when this guide was created

- Read AGENTS.md, reorganized solution, project targets, scripts, launch profile, appsettings, Compose file, authentication/provisioning controllers, customer routes/form, ticket controller and API client.
- Confirmed installed SDK/tool versions and working Docker engine/Compose; Docker was not needed or started for the test.
- Started the exact backend wrapper: migrations reported **No migrations were applied; database already up to date**, then API listened on 5011.
- Started the exact Vite command on 5173. GET `/login` and `/customer/requests/new` returned 200. Anonymous GET `/api/tickets` returned 401 directly on 5011 and through Vite on 5173.
- Read only the existing user count (seven); no account records, hashes, credentials or tokens were printed. No new accounts or tickets were created.
- Full manual authenticated browser scenario, activation and privileged ticket readback were **not executed**: no known development login was supplied. Visual/mobile checks remain for the tester. HTTP startup checks are not a claim of browser acceptance.
- Only the processes started for these checks were stopped afterward. No production code, credentials or dependency configuration changed. No commit, push or PR was made for this documentation request.

### Subsequent manual acceptance and delivery

The repository owner subsequently reported successful manual testing of the completed customer UI, including customer login and ticket submission, and authorized committing/pushing this guide with issue #103's existing feature branch and PR. This is owner-reported acceptance; the agent did not independently repeat that authenticated browser test. No ticket ID, screenshot or browser trace was supplied with the report. The earlier verification record above describes the guide-creation session, not the later manual acceptance or delivery status.

## Reusable checklist

- [ ] Prerequisites available; ports free or intended app identified.
- [ ] LocalDB development target confirmed; backend started on 5011.
- [ ] Frontend started on 5173; actual API base checked.
- [ ] Known active Customer login succeeds; dashboard appears.
- [ ] Protected form opens; Cancel returns without submission.
- [ ] Empty/whitespace validation works; native length limits verified.
- [ ] Exact scenario title/description and Medium priority entered.
- [ ] One POST sent; controls disabled while processing.
- [ ] HTTP 201 and response fields correct; no customer ID in request.
- [ ] Confirmation ID equals server-returned ID; next actions work.
- [ ] Privileged GET list independently finds that ID and exact fields.
- [ ] Anonymous browser/API and wrong-role access rejected.
- [ ] Optional expired-session/network checks completed without duplicate tickets.
- [ ] Browser/mobile layout inspected; results recorded without secrets.
- [ ] Sign out; stop only owned app processes and optionally owned LocalDB use.
