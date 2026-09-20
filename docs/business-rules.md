# ServiceDesk business rules

## Ticket rules

| ID | Rule |
|---|---|
| BR-001 | A ticket belongs to exactly one customer for its lifetime. Customer ownership is not normally changed after creation. |
| BR-002 | A ticket must have a non-empty title and description. |
| BR-003 | A ticket priority must be exactly one of `Low`, `Medium`, `High`, or `Critical`. |
| BR-004 | A ticket status must be exactly one of `Open`, `InProgress`, `Resolved`, or `Closed`. |
| BR-005 | A newly created ticket has status `Open`, immutable identity, and immutable creation timestamp. |
| BR-006 | A ticket may have no assigned employee when created. |
| BR-007 | A ticket may only be assigned or reassigned to an active employee. |
| BR-008 | Mutable ticket details are title, description, priority, assignment, and status. Every accepted material change updates `UpdatedAt`. |
| BR-009 | `ClosedAt` must be `null` whenever status is not `Closed`. |
| BR-010 | Closing a ticket is valid only from `Resolved`; it changes status to `Closed`, sets `ClosedAt`, updates `UpdatedAt`, and creates history. |
| BR-011 | Reopening is valid only from `Closed`; it changes status to `InProgress`, clears `ClosedAt`, updates `UpdatedAt`, and creates reopening history. |
| BR-012 | Returning to work is valid only from `Resolved`; it changes status to `InProgress`, keeps `ClosedAt` null, and creates history. |
| BR-013 | Starting work is valid only from `Open`; it changes status to `InProgress` and creates history. |
| BR-014 | Resolving is valid only from `InProgress`; it changes status to `Resolved` and creates history. |
| BR-015 | Invalid transitions—including `Open → Closed`, `Open → Resolved`, `Resolved → Open`, `Closed → Resolved`, and `Closed → Open`—must be rejected without changing ticket state or history. |

Status changes are explicit business operations because status determines whether work was performed, a solution was offered, or a solution was confirmed. They cannot be arbitrary field updates.

## Customer rules

| ID | Rule |
|---|---|
| BR-016 | A customer requires first name, last name, and a unique email; phone is optional. |
| BR-017 | Customer identity and `CreatedAt` are immutable. Contact details may change and update `UpdatedAt`. |
| BR-018 | A customer can have many tickets; a ticket cannot exist without its customer. |
| BR-019 | Customer changes must not retroactively alter ticket history. |

## Employee rules

| ID | Rule |
|---|---|
| BR-020 | An employee requires first name, last name, and a unique email. |
| BR-021 | Employee identity and `CreatedAt` are immutable; contact details and `IsActive` may change and update `UpdatedAt`. |
| BR-022 | An inactive employee cannot receive a new ticket assignment or reassignment. |
| BR-023 | Existing and historical tickets may retain their relationship to an employee who later becomes inactive. |
| BR-024 | An employee must not be physically deleted when doing so would break ticket or history information; deactivation is the normal alternative. |

## History rules

| ID | Rule |
|---|---|
| BR-025 | Every history entry belongs to exactly one ticket. |
| BR-026 | History is append-only: a stored entry is not normally edited or deleted. |
| BR-027 | A history entry records the ticket, actor, occurrence time, action type, and relevant change information. |
| BR-028 | Ticket creation, assignment/reassignment, priority change, status change, comment addition, title change, description change, closure, and reopening must create history. |
| BR-029 | A comment is represented as a history entry attributable to the actor who added it. |
| BR-030 | A rejected operation creates no history entry. |

## Integrity and concurrency rules

| ID | Rule |
|---|---|
| BR-031 | Customer email and employee email must be unique. |
| BR-032 | Ticket customer, assigned-employee, and history-ticket relationships must be referentially valid; assigned employee is optional. |
| BR-033 | A stale ticket update must fail with a concurrency conflict instead of overwriting a later accepted update. |
| BR-034 | History and status/closure changes for one accepted operation must remain consistent: no successful status change may exist without its required history record. |

## Authorization boundary

The business rules describe what is allowed for valid records. Authentication credentials, tokens, and password storage are not Domain concepts. Future authorization determines which authenticated actor may invoke an operation; it must enforce these business rules rather than replace them.

## Open policy decisions

- Who is permitted to close versus only resolve a ticket.
- Whether customer-created tickets are exposed directly at initial release or via an authenticated customer portal later.
- Customer retention/deactivation policy.
- Field-length limits and whether comments have a maximum size.
