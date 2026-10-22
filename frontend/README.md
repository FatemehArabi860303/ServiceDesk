# ServiceDesk frontend

Use the existing React/TypeScript application for both administrator and customer sign-in.

## Local development

From `frontend`, run `npm ci` and `npm run dev`. Start the existing WebApi on
`http://localhost:5011` using the repository's local setup/run scripts. The Vite server
uses `http://localhost:5173` and proxies `/api` to the WebApi. `VITE_API_BASE_URL` can
select a different API base; for cross-origin access, configure that frontend origin
in the existing WebApi CORS allowlist. Never put secrets in `VITE_` variables: they
are public frontend configuration.

Sign in at `/login` with an active customer account. Customer routes are `/customer`
and `/customer/requests/new`. Existing administrator routes are unchanged. Session
storage and bearer authentication reuse the existing auth context and API client;
the API remains the authority for permissions and customer identity.

The request form sends `title`, `description`, and the contract's numeric `priority`
(Low 0, Medium 1, High 2, Critical 3) to `POST /api/tickets`. It sends no customer ID.
Only a confirmed creation response with a ticket identifier displays success. The
form does not call OpenAI or wait for AI analysis; no worker/broker is needed for
submission while SQL/WebApi are available.

## Development customer setup

No customer credentials are committed or assumed to exist. Prefer an existing
development customer. Otherwise use an isolated development database and the
supported procedures in
[`secure-servicedesk-access-manual-api-test.md`](../scripts/secure-servicedesk-access-manual-api-test.md):

1. Bootstrap the first administrator only on an empty installation using protected
   environment configuration and the existing bootstrap command/script.
2. Sign in as that administrator through `POST /api/auth/login`.
3. Create a customer through `POST /api/users` with role `0`.
4. Provision access through `POST /api/users/{id}/access-provisioning`.
5. Activate through `POST /api/auth/activate` with the returned activation token
   and a securely chosen development password; then use that customer on `/login`.

Supply passwords/tokens through secure local input/environment, never source files,
command history, screenshots or logs. Do not bootstrap or seed a shared installation
for testing. Dispose of test-owned infrastructure after acceptance.

## Verification

- `npm run typecheck`
- `npm test -- --run` (deterministic frontend suite)
- `npm run build`
- No lint script or linter is configured in the existing application.

An explicit real-HTTP UI integration check is also available:

1. Start an isolated WebApi/SQL database and provision a development customer as above.
2. Securely set process environment variables `VITE_API_BASE_URL`,
   `SERVICEDESK_TEST_CUSTOMER_EMAIL`, and `SERVICEDESK_TEST_CUSTOMER_PASSWORD`.
3. Run `npm run test:live` from `frontend`.

This check renders the actual React login/dashboard/form in JSDOM and uses real HTTP
through the existing API client, with no authentication or submission mocks. It
creates one synthetic ticket, displays its server-returned identifier, and prints
only that identifier. It does not call OpenAI. It is separate from the deterministic
suite and must only target development data. JSDOM does not verify browser CORS,
visual layout, or mobile rendering; inspect those in an enabled browser separately.

## Issue #103 verification record

The customer-flow tests and real-HTTP integration passed with a generated disposable
customer in an isolated SQL container. The returned identifier matched durable SQL
identity, a pending Outbox existed without RabbitMQ/AI, forged customer identity was
ignored, and anonymous/administrator submissions were rejected. Cleanup passed;
no provider requests were made. Browser screenshots/visual verification were
unavailable in the implementation session.

The full frontend suite has one pre-existing failure in
`src/components/users/__tests__/UsersPage.test.tsx`: the provisioning-conflict message
assertion expects exact text without the rendered `Error:` prefix. This same failure
was reproduced from untouched `main` (`11c2aa7`); it is outside the customer-UI scope.
