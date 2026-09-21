# ServiceDesk system overview

## Purpose

ServiceDesk is a small internal IT service desk and ticket-management system. It records support requests from customers, lets support employees work those requests, and preserves an auditable history of important ticket activity. The first version is a REST API for a single organisation; a separate user interface is outside this scope.

The system is intended to be practical for a small business: it prioritises clear ticket ownership, controlled status changes, reliable audit information, and straightforward administration over broad ITSM features.

## Actors

### Customer

A customer is a person needing IT support. A customer can create and view their own tickets, provide additional information through comments, view current ticket status, and view ticket history. The API's future authorization design must ensure customers cannot access another customer's tickets.

### Employee / support agent

An employee handles support tickets. An employee can view assigned work, work tickets, update status and priority, add comments/history, resolve tickets, and be assigned or reassigned to tickets.

### Administrator

An administrator manages customers and employees, assigns or reassigns tickets, and can access tickets and their history. Administrator access does not bypass ticket lifecycle rules.

No other actor is part of version one.

## Core concepts

| Concept | Meaning |
|---|---|
| Customer | The person for whom a ticket is recorded. A customer has many tickets. |
| Employee | A support worker who may be assigned to tickets. |
| Ticket | One support request belonging to exactly one customer. |
| Ticket status | The current stage of work: `Open`, `InProgress`, `Resolved`, or `Closed`. |
| Ticket priority | Work urgency: `Low`, `Medium`, `High`, or `Critical`. |
| Ticket history | The append-only audit trail of meaningful ticket activity. |

## Approved target user-management model

> **Target model — not implemented in the current codebase.**

A `User` is the common ServiceDesk participant identity and business-eligibility record. A User owns first name, last name, email, one fixed role, active state, and creation/update timestamps. The only supported roles are `Customer`, `Employee`, and `Administrator`.

An administrator creates a complete User with exactly one supported role; the User is active by default. Administrators later maintain identifying information, role, and active state. Customer and Employee are the role-specific business concepts used for requesting and delivering support. An Administrator requires no separate business profile.

The current implementation still has independent `Customer` and `Employee` entities that each contain identifying information. Their eventual relationship to User is intentionally not implemented yet. The target direction is for User to own the common participant identity and email, avoiding duplicated identity data when the User/profile relationship is introduced.

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

A ticket conceptually contains its customer, optional assigned employee, status, priority, title, description, creation/update/closure times, and history.

- `Customer`, `CreatedAt`, and the ticket identity are immutable after creation.
- `AssignedEmployee`, `Status`, `Priority`, `Title`, and `Description` may change through defined business operations.
- `UpdatedAt` changes when mutable ticket information, assignment, priority, status, or a comment changes.
- `ClosedAt` is `null` unless status is `Closed`; it is set on closing and cleared only by reopening.
- A ticket may begin unassigned. It may later be assigned or reassigned only to an active employee.
- Title and description are required; status and priority must be values from their defined sets.

## Ticket history

History is an append-only audit trail, not a second editable ticket description. A record conceptually identifies the ticket, the actor who performed the action, the time, the action type, and a concise account of what changed. Where useful it stores prior and new values, such as old/new assignee, priority, or status.

Comments are represented as history entries with their author and comment text. The following operations must create history: ticket creation, assignment/reassignment, priority change, status change, adding a comment, title change, description change, closing, and reopening. Existing history is not normally edited or deleted.

## Customer and employee behavior

Customers have an immutable identity and creation time; first name, last name, email, phone, and update time may change. First name, last name, and email are required; phone is optional. Customer email is unique. A customer has many tickets, while every ticket has exactly one customer. Moving an existing ticket to a different customer is not a normal supported operation.

Employees have an immutable identity and creation time; first name, last name, email, active state, and update time may change. First name, last name, and email are required; email is unique. Inactive employees cannot receive new assignments, but historical and existing ticket references remain valid. Employees are deactivated rather than physically deleted when deletion would damage historical information.

## Authentication boundary

User management is separate from authentication. A User is a business participant identity, not authentication infrastructure. Passwords, password hashing, JWTs, login, refresh tokens, identity persistence, and authentication middleware remain outside this scope. `Customer` and `Employee` business profiles are not automatically login accounts.

If a User later authenticates, authentication identity should be associated with that User without placing credential or token mechanics in the Domain model.

## Scope boundaries

Version one excludes email/SMS notifications, attachments, SLA management, escalation engines, knowledge base, asset/change/problem management, multi-tenancy, real-time chat, AI classification, microservices, and distributed event infrastructure. These may be evaluated later only if business needs justify them.

## Assumptions and open decisions

1. The system serves one organisation; multi-tenancy is out of scope.
2. Customer and employee email addresses are unique system-wide.
3. Assignment does not itself force a status change; starting work is explicit.
4. The authorization details for who may create a ticket, close it, or confirm resolution need approval when authentication requirements are introduced.
5. No deletion policy for customers is fixed yet; it must preserve ticket integrity and history.
