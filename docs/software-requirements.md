# ServiceDesk software requirements

## Functional requirements

### Customer management

| ID | Requirement |
|---|---|
| FR-001 | The system shall create a customer with required first name, last name, and unique email; phone is optional. |
| FR-002 | The system shall retrieve a customer by identifier. |
| FR-003 | The system shall update mutable customer contact details while preserving customer identity and creation time. |
| FR-004 | The system shall retrieve the tickets belonging to a customer. |

### Employee management

| ID | Requirement |
|---|---|
| FR-005 | The system shall create an employee with required first name, last name, unique email, and active state. |
| FR-006 | The system shall retrieve an employee by identifier. |
| FR-007 | The system shall update mutable employee details while preserving employee identity and creation time. |
| FR-008 | The system shall activate or deactivate an employee. |
| FR-009 | The system shall retrieve tickets assigned to an employee, including tickets retained for an employee who later becomes inactive. |

### Ticket management

| ID | Requirement |
|---|---|
| FR-010 | The system shall create a ticket for exactly one existing customer, with required title, description, and valid priority. New tickets start `Open` and may be unassigned. |
| FR-011 | The system shall retrieve a ticket and its current details. |
| FR-012 | The system shall list tickets and support practical filtering/searching by customer, assigned employee, status, priority, and text; results shall be pageable. |
| FR-013 | The system shall update permitted ticket information, including title and description, without changing its customer ownership. |
| FR-014 | The system shall change a ticket priority to a valid defined value and record the change. |
| FR-015 | The system shall assign an active employee to an unassigned ticket and record the assignment. |
| FR-016 | The system shall reassign a ticket to another active employee and record prior and new assignees. |
| FR-017 | The system shall change status only through defined valid lifecycle transitions and record the change. |
| FR-018 | The system shall add comments as attributable, immutable history entries. |
| FR-019 | The system shall retrieve a ticket's history in chronological order. |
| FR-020 | The system shall resolve an `InProgress` ticket, changing it to `Resolved`. |
| FR-021 | The system shall close a `Resolved` ticket, changing it to `Closed`, setting `ClosedAt`, and recording closure. |
| FR-022 | The system shall return a `Resolved` ticket to `InProgress` when more work is needed. |
| FR-023 | The system shall reopen a `Closed` ticket to `InProgress`, clear `ClosedAt`, and record reopening. |
| FR-024 | The system shall reject invalid status transitions without changing the ticket or creating history. |

## Non-functional requirements

| ID | Requirement |
|---|---|
| NFR-001 | The system shall expose a RESTful HTTP API using JSON request and response bodies. |
| NFR-002 | The API shall use meaningful HTTP status codes and consistent problem-details error responses. |
| NFR-003 | The API shall use controllers as its endpoint style and publish an OpenAPI/Swagger description in appropriate environments. |
| NFR-004 | The production persistence store shall be SQL Server, accessed through Entity Framework Core. |
| NFR-005 | Inputs shall be validated before a business operation executes; invalid input shall be rejected with actionable validation details. |
| NFR-006 | Important operations, failures, and request context shall be logged using structured logging without recording credentials or tokens. |
| NFR-007 | Authentication and role-based authorization are future capabilities and shall be designed without placing credential mechanics in the Domain layer. |
| NFR-008 | Domain rules and application workflows shall have automated unit tests; HTTP behavior and persistence interactions shall have integration tests. |
| NFR-009 | Ticket updates shall use optimistic concurrency so stale changes cause a conflict rather than silently overwriting newer data. |
| NFR-010 | The solution shall remain a modular monolith with Domain, UseCases, Infrastructure, and WebApi projects; it shall not introduce distributed infrastructure without a demonstrated need. |

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

1. An unassigned ticket is assigned to Employee A.
2. It is reassigned to Employee B.
3. History records both changes, including prior and new assignment where applicable.

### Scenario F — Inactive employee

1. An employee is inactive.
2. An actor attempts to assign a new ticket to that employee.
3. The operation is rejected and the assignment remains unchanged.

### Scenario G — Concurrent update

1. User A and User B retrieve the same ticket version.
2. User A updates it successfully.
3. User B submits an update based on the earlier version.
4. The system reports a concurrency conflict and does not overwrite User A's change.

## Database requirements

All major records require primary keys. Required foreign-key relationships are `Ticket.CustomerId → Customer.Id`, nullable `Ticket.AssignedEmployeeId → Employee.Id`, and `TicketHistory.TicketId → Ticket.Id`. A history record also needs an actor reference appropriate to the eventual authorization model.

Customer and employee email must be unique. Useful indexes are customer email and employee email (uniqueness/lookups); ticket customer and assigned employee (customer and agent work lists); ticket status and priority (filtering queues); ticket creation date (sorting/reporting); and ticket-history ticket/date (chronological audit retrieval). Indexes should be created only for these expected query patterns and reviewed when real usage changes.

The database must preserve references needed for ticket and history integrity. In particular, deleting an employee must not orphan historical information, and `AssignedEmployeeId` remains nullable for unassigned tickets.

## Concurrency requirement

Ticket changes must be protected from lost updates. If two users read version 5, one successfully updates to version 6, and the other submits version 5, the latter request must fail with a conflict. The client must retrieve current state before deciding whether to retry. The concrete persistence mechanism is intentionally not specified here.

## Deliberately undecided

- Exact maximum lengths and formatting rules for names, title, description, phone, and comments.
- Whether customer self-service is included in the first API release or introduced with later authentication.
- Exact role-to-operation permissions and who confirms a resolution.
- Whether customers may ever be deactivated or deleted; any policy must preserve tickets and history.
