# ServiceDesk system overview

## Purpose

ServiceDesk is a small internal IT service desk and ticket-management system. It records support requests from customers, lets support employees work those requests, and preserves an auditable history of important ticket activity. The first version is a REST API for a single organisation; a separate user interface is outside this scope.

The system is intended to be practical for a small business: it prioritises clear ticket ownership, controlled status changes, reliable audit information, and straightforward administration over broad ITSM features.

## Actors

### Customer

A customer is a User whose role is `Customer` and who needs IT support. A customer submits and owns service requests, and may later view their own requests, conversation, status, and history. Future authorization must ensure a customer cannot access another customer's requests.

### Employee / support agent

An employee is a User whose role is `Employee`. An employee handles support requests, including assigned work, lifecycle updates, priority changes, and comments/history.

### Administrator

An administrator is a User whose role is `Administrator`. An administrator manages Users and ServiceDesk settings, assigns or reassigns requests, and can access requests and their history. Administrator access does not bypass ticket lifecycle rules.

No other actor is part of version one.

## Core concepts

| Concept | Meaning |
|---|---|
| User | The system identity with one role: `Customer`, `Employee`, or `Administrator`. |
| Customer | A User with the `Customer` role who submits and owns requests. |
| Employee | A User with the `Employee` role who may be assigned to requests. |
| Ticket | One support request belonging to exactly one customer User. |
| Ticket status | The current stage of work: `Open`, `InProgress`, `Resolved`, or `Closed`. |
| Ticket priority | Work urgency: `Low`, `Medium`, `High`, or `Critical`. |
| Ticket history | The append-only audit trail of meaningful ticket activity. |

## User identity model

A `User` is the common ServiceDesk participant identity and business-eligibility record. A User owns first name, last name, email, one fixed role, active state, and creation/update timestamps. The only supported roles are `Customer`, `Employee`, and `Administrator`.

An administrator creates a complete User with exactly one supported role; the User is active by default. Customer, Employee, and Administrator are role meanings, not separate identity entities. No separate Customer or Employee identity profile is introduced.

## Ticket lifecycle

```text
Open → InProgress → Resolved → Closed
                  ↑       │
                  └───────┘
Closed ───────────────────→ InProgress
```

| From | To | Business operation | Result |
|---|---|---|---|
| — | Open | Create ticket | A new ticket starts Open. |
| Open | InProgress | Start work / take ownership | Work has begun. |
| InProgress | Resolved | Resolve ticket | Agent reports the issue resolved. |
| Resolved | Closed | Close ticket | Resolution is confirmed; `ClosedAt` is set. |
| Resolved | InProgress | Return to work | Resolution did not solve the issue. |
| Closed | InProgress | Reopen ticket | A closed issue needs further work; `ClosedAt` is cleared. |

The following transitions are invalid: `Open → Closed`, `Open → Resolved`, `Resolved → Open`, `Closed → Resolved`, and `Closed → Open`. A ticket does not skip work or confirmation stages, and reopening is an operation, not a separate status.

Every successful status operation creates a history record. Closing creates a **ticket closed** record and sets `ClosedAt`; reopening creates a **ticket reopened** record and clears it. Other valid changes create a **status changed** record containing old and new statuses.

## Ticket behavior

A future ticket conceptually contains `CustomerUserId`, optional `AssignedEmployeeUserId`, status, priority, title, description, creation/update/closure times, and history.

- `CustomerUserId`, `CreatedAt`, and the ticket identity are immutable after creation. `CustomerUserId` must reference a User with the `Customer` role.
- `AssignedEmployeeUserId`, `Status`, `Priority`, `Title`, and `Description` may change through defined business operations. When assigned, `AssignedEmployeeUserId` must reference a User with the `Employee` role.
- `UpdatedAt` changes when mutable ticket information, assignment, priority, status, or a comment changes.
- `ClosedAt` is `null` unless status is `Closed`; it is set on closing and cleared only by reopening.
- A ticket may begin unassigned. It may later be assigned or reassigned only to an active employee.
- Title and description are required; status and priority must be values from their defined sets.

## Ticket history

History is an append-only audit trail, not a second editable ticket description. A future record identifies the ticket, `ActorUserId`, occurrence time, action type, and a concise account of what changed. For ticket creation, `ActorUserId` equals `CustomerUserId`.

Comments are represented as history entries with their author and comment text. The following operations must create history: ticket creation, assignment/reassignment, priority change, status change, adding a comment, title change, description change, closing, and reopening. Existing history is not normally edited or deleted.

## Customer and employee role behavior

A customer User owns many tickets, while every ticket has exactly one customer User. Moving an existing ticket to a different customer User is not a normal supported operation.

An employee User may be assigned to tickets. Inactive employee Users cannot receive new assignments, but historical and existing ticket references remain valid. Employee Users are deactivated rather than physically deleted when deletion would damage historical information.

## ServiceDesk settings

ServiceDesk settings will include a future configurable hierarchical concept named `Feature`. A Feature has a name, description, and child Features. It can represent classifications such as Software → Installation or Hardware → Repair, with deeper structures possible. Features may later classify service requests, but the Ticket/Feature relationship and all persistence, validation, hierarchy, and lifecycle details are intentionally deferred to the Update Settings work.

## Authentication boundary

User management is separate from authentication. A User remains the authoritative business participant identity and owns one fixed role, but credentials are not part of the Functional Core User model. Authentication uses the User's unique email as its login identifier and immutable User identity as the authenticated identity.

ServiceDesk will use self-issued JWT bearer authentication. A separate `UserCredential` record associates a User with only `UserId` and `PasswordHash`. Passwords are never stored in plaintext. JWTs, password hashing, credential persistence, login, and authentication middleware remain outside the Functional Core.

Passwords are a single authentication factor. They must contain 15 to 128 Unicode code points, may contain Unicode and spaces, are normalized to NFC before hashing and verification, and are neither trimmed nor silently truncated. No character-composition rule or periodic expiration applies.

Initial installation requires an explicit deployment/setup seed operation, not automatic startup behavior. On an empty installation only, it creates the first Administrator User and credential using protected deployment configuration or secrets. Later, an authenticated active Administrator may initialize a credential for an existing active User that has no credential. Credential initialization does not replace an existing credential or modify the User's identity, profile, or role.

The operational sequence is: explicitly seed the first Administrator, authenticate that Administrator, provision credentials for existing Users, authenticate those Users, then allow protected operations such as customer ticket submission. This setup behavior is not a public product endpoint.

## Scope boundaries

Version one excludes email/SMS notifications, attachments, SLA management, escalation engines, knowledge base, asset/change/problem management, multi-tenancy, real-time chat, AI classification, microservices, and distributed event infrastructure. These may be evaluated later only if business needs justify them.

## Assumptions and open decisions

1. The system serves one organisation; multi-tenancy is out of scope.
2. User email addresses are unique system-wide.
3. Assignment does not itself force a status change; starting work is explicit.
4. Detailed authorization policies beyond approved authentication and credential provisioning, including who may close or confirm a ticket resolution, remain to be defined.
5. No deletion policy for Users is fixed yet; it must preserve ticket integrity and history.
