# ServiceDesk software requirements

## Functional requirements

### User management

| ID | Requirement |
|---|---|
| UM-001 | An administrator shall create a User with required first name, last name, unique email, and exactly one role: `Customer`, `Employee`, or `Administrator`. A new User shall be active by default. |
| UM-002 | An administrator shall update a User's first name, last name, email, role, and active state while preserving identity and timestamps as appropriate. |
| UM-003 | User management shall not require passwords, login, JWTs, or other authentication mechanisms. |

Customer, Employee, and Administrator are User roles, not separate identity entities. User.Email is the common participant email and is unique across Users.

### Secure ServiceDesk Access

| ID | Requirement |
|---|---|
| AUTH-001 | ServiceDesk shall use self-issued JWT bearer authentication. A successful login shall identify the immutable User.Id and current User.Role in a signed, expiring token. The initial access-token lifetime shall be four hours. A valid token is authoritative until expiration; protected operations shall not reload the persisted User solely to re-check role or active state. |
| AUTH-002 | Login shall use the User's unique email and password. A User must exist, be active, have a credential, and verify the password. Unknown email, missing credential, invalid password, and inactive User failures shall be externally generic. |
| AUTH-003 | Authentication credentials shall be persisted separately from User management as UserCredential data containing only UserId and PasswordHash. Passwords shall never be persisted in plaintext. |
| AUTH-004 | Passwords shall contain 15 to 128 Unicode code points; Unicode and spaces are allowed; NFC normalization shall occur before hashing and verification; passwords shall not be trimmed or silently truncated; no composition rule or periodic expiration applies. |
| AUTH-005 | An authenticated active Administrator shall provision access only for an existing active User with no UserCredential through `POST /api/users/{userId}/access-provisioning`. Provisioning shall create a cryptographically secure one-time activation token, return its raw value only at provisioning time, and persist only a non-recoverable representation. Provisioning shall not involve a password, replace a credential, or modify the User's identity, profile, or role. |
| AUTH-006 | An explicit deployment/setup seed operation shall create the first Administrator User and credential only for an empty installation, using protected deployment configuration or secrets. It shall reject subsequent bootstrap attempts. |
| AUTH-007 | JWT issuer, audience, signature, and expiration shall be validated. Signing material shall come from protected deployment configuration, and passwords, hashes, and tokens shall not be logged. |
| AUTH-008 | A provisioned User shall activate their account without an existing JWT by supplying a valid unused activation token and a password they choose. Activation shall validate and normalize the password under AUTH-004, create a UserCredential, and consume the activation token atomically. |
| AUTH-009 | Activation tokens shall be cryptographically generated, expire exactly 24 hours after provisioning, be non-recoverable from persisted data, never logged, and be usable only once. An authenticated active Administrator may re-provision an active User who still has no UserCredential; re-provisioning shall replace the pending provision and invalidate the previous token immediately. |

### Customer role behavior

| ID | Requirement |
|---|---|
| FR-001 | A User with the `Customer` role shall be the requester and owner of a service request. |
| FR-002 | The system shall later retrieve the tickets belonging to a Customer User. |

### Employee role behavior

| ID | Requirement |
|---|---|
| FR-005 | A User with the `Employee` role shall be eligible to handle service requests. |
| FR-006 | The system shall later retrieve tickets assigned to an Employee User, including tickets retained for an employee who later becomes inactive. |

Until future Feature/category eligibility is configured, an authenticated Employee may use the complete ticket list to identify available unassigned tickets and use visible information such as Priority to choose a request. An available-ticket view and filtering remain separate from baseline retrieval.

### Ticket management

| ID | Requirement |
|---|---|
| FR-010 | The system shall create a ticket for exactly one existing Customer User, with required title, description, and valid priority. New tickets start `Open` and may be unassigned. |
| FR-011 | The system shall retrieve a ticket and its current details. |
| FR-012 | An authenticated Employee or Administrator shall retrieve all tickets. Filtering/searching by Customer User, assigned Employee User, status, priority, and text, together with pagination and other list refinements, is deferred to separate work. |
| FR-013 | The system shall update permitted ticket information, including title and description, without changing its Customer User ownership. |
| FR-014 | The system shall change a ticket priority to a valid defined value and record the change. |
| FR-015 | An authenticated Employee shall self-assign an `Open`, unassigned ticket to the Employee User identified by the authenticated access token. The operation shall record the assignment, update the ticket timestamp, and not change ticket status. |
| FR-016 | The system shall later support reassignment to another Employee User and record prior and new assignees. Reassignment is separate from Employee self-assignment; its actor authorization and detailed rules require separate design. |
| FR-017 | The system shall change status only through defined valid lifecycle transitions and record the change. |
| FR-018 | The system shall add comments as attributable, immutable history entries. |
| FR-019 | The system shall retrieve a ticket's history in chronological order. |
| FR-020 | The system shall resolve an `InProgress` ticket, changing it to `Resolved`. |
| FR-021 | The system shall close a `Resolved` ticket, changing it to `Closed`, setting `ClosedAt`, and recording closure. |
| FR-022 | The system shall return a `Resolved` ticket to `InProgress` when more work is needed. |
| FR-023 | The system shall reopen a `Closed` ticket to `InProgress`, clear `ClosedAt`, and record reopening. |
| FR-024 | The system shall reject invalid status transitions without changing the ticket or creating history. |
| FR-025 | An authenticated Employee assigned to an `Open` ticket shall start work on that ticket. The operation shall change the status to `InProgress`, preserve the assignment, update the ticket timestamp, and record work-started history. |

### ServiceDesk settings

| ID | Requirement |
|---|---|
| CFG-001 | An administrator shall update ServiceDesk settings. |
| CFG-002 | Settings shall include a future configurable hierarchical concept named `Feature`, with a name, description, and child Features. |
| CFG-003 | Features may later classify service requests; the Ticket/Feature relationship is intentionally deferred to the future Update Settings design. |

## Non-functional requirements

| ID | Requirement |
|---|---|
| NFR-001 | The system shall expose a RESTful HTTP API using JSON request and response bodies. |
| NFR-002 | The API shall use meaningful HTTP status codes and consistent problem-details error responses. |
| NFR-003 | The API shall use controllers as its endpoint style and publish an OpenAPI/Swagger description in appropriate environments. |
| NFR-004 | The production persistence store shall be SQL Server, accessed through Entity Framework Core. |
| NFR-005 | Inputs shall be validated before a business operation executes; invalid input shall be rejected with actionable validation details. |
| NFR-006 | Important operations, failures, and request context shall be logged using structured logging without recording credentials or tokens. |
| NFR-007 | User management and authentication shall remain separate. Authentication and role-based authorization shall use the existing User identity and role without placing credential, password-hashing, or JWT mechanics in the Functional Core. |
| NFR-008 | Functional Core rules and Shell workflows shall have automated unit tests; HTTP behavior and persistence interactions shall have integration tests. |
| NFR-009 | Ticket updates shall use optimistic concurrency so stale changes cause a conflict rather than silently overwriting newer data. |
| NFR-010 | The solution shall remain a modular monolith with Core, Shell, Repository, and WebApi projects; it shall not introduce distributed infrastructure without a demonstrated need. |

## Acceptance scenarios

### Scenario A — Normal lifecycle

1. A customer creates a ticket; it is `Open`.
2. An employee takes ownership and starts work; it becomes `InProgress`.
3. The employee adds work information; a comment/history entry is recorded.
4. The employee resolves it; it becomes `Resolved`.
5. Resolution is confirmed and the ticket is closed; it becomes `Closed`, `ClosedAt` is populated, and history records closure.

### Scenario B — Resolution is not effective

1. A `Resolved` ticket is returned to `InProgress`.
2. Further work and history are added.
3. The ticket is resolved and later closed through normal valid transitions.

### Scenario C — Closed ticket reopened

1. A `Closed` ticket is reopened.
2. Its status becomes `InProgress`.
3. `ClosedAt` becomes `null`.
4. History records the reopening and the old/new status.

### Scenario D — Invalid transition

1. A ticket is `Open`.
2. An actor attempts `Open → Closed`.
3. The operation is rejected, no history is added, and the ticket remains `Open`.

### Scenario E — Assignment audit

1. Employee A self-assigns an `Open`, unassigned ticket.
2. It is later reassigned to Employee B by a future reassignment capability.
3. History records both changes, including prior and new assignment where applicable.

### Scenario F — Access-token authority after User state change

1. An Employee authenticates while active and receives an access token.
2. An administrator changes the persisted User active state or role.
3. The Employee self-assigns an `Open`, unassigned ticket while the Employee access token remains valid.
4. The assignment is accepted; the persisted User state will affect a later login, not the already-issued token.

### Scenario G — Concurrent update

1. User A and User B retrieve the same ticket version.
2. User A updates it successfully.
3. User B submits an update based on the earlier version.
4. The system reports a concurrency conflict and does not overwrite User A's change.

## Database requirements

All major records require primary keys. Required future foreign-key relationships are `Ticket.CustomerUserId → User.Id`, nullable `Ticket.AssignedEmployeeUserId → User.Id`, `TicketHistory.TicketId → Ticket.Id`, `TicketHistory.ActorUserId → User.Id`, and nullable `TicketHistory.AssignedEmployeeUserId → User.Id` for assignment history. Ticket creation records the submitting customer as both `CustomerUserId` and `ActorUserId`; self-assignment records the authenticated Employee as both history actor and assigned Employee.

User email must be unique. Useful indexes include ticket customer and assigned employee Users (customer and agent work lists); ticket status and priority (filtering queues); ticket creation date (sorting/reporting); and ticket-history ticket/date (chronological audit retrieval). Indexes should be created only for these expected query patterns and reviewed when real usage changes.

The database must preserve references needed for ticket and history integrity. In particular, deleting an Employee User must not orphan historical information, and `AssignedEmployeeUserId` remains nullable for unassigned tickets.

## Concurrency requirement

Ticket changes must be protected from lost updates. For self-assignment, two Employees attempting to claim the same `Open`, unassigned ticket must result in exactly one accepted assignment; the losing request must fail with a conflict and create no history. The concrete persistence mechanism is intentionally not specified here.

## Deliberately undecided

- Exact maximum lengths and formatting rules for names, title, description, and comments.
- Exact role-to-operation permissions beyond Employee self-assignment, and who confirms a resolution.
- Whether Users may ever be deactivated or deleted; any policy must preserve tickets and history.
