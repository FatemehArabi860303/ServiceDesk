# ServiceDesk business rules

## Ticket rules

| ID | Rule |
|---|---|
| BR-001 | A ticket belongs to exactly one Customer User for its lifetime through `CustomerUserId`. Customer ownership is not normally changed after creation. |
| BR-002 | A ticket must have a non-empty title and description. |
| BR-003 | A ticket priority must be exactly one of `Low`, `Medium`, `High`, or `Critical`. |
| BR-004 | A ticket status must be exactly one of `Open`, `InProgress`, `Resolved`, or `Closed`. |
| BR-005 | A newly created ticket has status `Open`, immutable identity, and immutable creation timestamp. |
| BR-006 | A ticket may have no assigned Employee User when created. |
| BR-007 | A ticket may only be assigned or reassigned to an active Employee User. |
| BR-008 | Mutable ticket details are title, description, priority, assignment, and status. Every accepted material change updates `UpdatedAt`. |
| BR-009 | `ClosedAt` must be `null` whenever status is not `Closed`. |
| BR-010 | Closing a ticket is valid only from `Resolved`; it changes status to `Closed`, sets `ClosedAt`, updates `UpdatedAt`, and creates history. |
| BR-011 | Reopening is valid only from `Closed`; it changes status to `InProgress`, clears `ClosedAt`, updates `UpdatedAt`, and creates reopening history. |
| BR-012 | Returning to work is valid only from `Resolved`; it changes status to `InProgress`, keeps `ClosedAt` null, and creates history. |
| BR-013 | Starting work is valid only from `Open`; it changes status to `InProgress` and creates history. |
| BR-014 | Resolving is valid only from `InProgress`; it changes status to `Resolved` and creates history. |
| BR-015 | Invalid transitions—including `Open → Closed`, `Open → Resolved`, `Resolved → Open`, `Closed → Resolved`, and `Closed → Open`—must be rejected without changing ticket state or history. |

Status changes are explicit business operations because status determines whether work was performed, a solution was offered, or a solution was confirmed. They cannot be arbitrary field updates.

## Customer role rules

| ID | Rule |
|---|---|
| BR-016 | A Customer is a User with the `Customer` role; no separate Customer identity entity exists. |
| BR-017 | A Customer User can have many tickets; a ticket cannot exist without its Customer User. |
| BR-018 | A ticket's `CustomerUserId` is immutable after creation. |
| BR-019 | User changes must not retroactively alter ticket history. |

## Employee role rules

| ID | Rule |
|---|---|
| BR-020 | An Employee is a User with the `Employee` role; no separate Employee identity entity exists. |
| BR-021 | An inactive Employee User cannot receive a new ticket assignment or reassignment. |
| BR-022 | Existing and historical tickets may retain their relationship to an Employee User who later becomes inactive. |
| BR-023 | An Employee User must not be physically deleted when doing so would break ticket or history information; deactivation is the normal alternative. |

## User rules

| ID | Rule |
|---|---|
| UR-001 | A User requires first name, last name, email, and exactly one role: `Customer`, `Employee`, or `Administrator`. |
| UR-002 | User email is unique across Users. |
| UR-003 | A newly created User is active by default. |
| UR-004 | An administrator may later update User identifying information, role, and active state. Unsupported roles are rejected. |
| UR-005 | Role changes must preserve historical ticket and history data; destructive identity handling is not defined. |
| UR-006 | User management does not create authentication credentials or login behavior. |

Customer, Employee, and Administrator are User roles, not separate identity entities.

## History rules

| ID | Rule |
|---|---|
| BR-025 | Every history entry belongs to exactly one ticket. |
| BR-026 | History is append-only: a stored entry is not normally edited or deleted. |
| BR-027 | A history entry records the ticket, `ActorUserId`, occurrence time, action type, and relevant change information. |
| BR-028 | Ticket creation, assignment/reassignment, priority change, status change, comment addition, title change, description change, closure, and reopening must create history. Ticket creation records `ActorUserId` equal to `CustomerUserId`. |
| BR-029 | A comment is represented as a history entry attributable to the User who added it. |
| BR-030 | A rejected operation creates no history entry. |

## Integrity and concurrency rules

| ID | Rule |
|---|---|
| BR-031 | User email must be unique. |
| BR-032 | Ticket customer User, assigned Employee User, history-ticket, and history-actor User relationships must be referentially valid; assigned Employee User is optional. |
| BR-033 | A stale ticket update must fail with a concurrency conflict instead of overwriting a later accepted update. |
| BR-034 | History and status/closure changes for one accepted operation must remain consistent: no successful status change may exist without its required history record. |

## Authorization boundary

The business rules describe what is allowed for valid records. Authentication credentials, tokens, and password storage are not Functional Core concepts. User management is likewise separate from authentication. Future authorization determines which authenticated User may invoke an operation; it must enforce these business rules rather than replace them.

## Future ServiceDesk settings

`Feature` is a future configurable, hierarchical ServiceDesk setting. A Feature has a name, description, and child Features; it may represent structures such as Software → Installation or Hardware → Repair. Feature is recursive rather than separate Category and Subcategory concepts. Features may later classify tickets, but their relationship to tickets and all persistence, validation, hierarchy, and lifecycle decisions are deferred to the future Update Settings work.

## Open policy decisions

- Who is permitted to close versus only resolve a ticket.
- Whether customer-created tickets are exposed directly at initial release or via an authenticated customer portal later.
- User retention/deactivation policy.
- Field-length limits and whether comments have a maximum size.
