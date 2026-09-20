# ServiceDesk Development Workflow

This repository uses an issue-driven, pull-request-only development workflow.

These rules are mandatory for all code changes.

## 1. Core Rule

Never implement a development request directly on `main`.

Every development request must follow this lifecycle:

```text
User Request
→ GitHub Issue / Backlog Item
→ Working Branch
→ Implementation
→ Tests
→ Commit(s)
→ Push
→ Pull Request
→ Review / Approval
→ Merge
```

Do not bypass any step unless the user explicitly instructs you to do so.

## 2. Backlog / GitHub Issue

Before modifying production code, every development request must have a corresponding GitHub Issue.

If no issue exists:

1. Create a GitHub Issue before implementation.
2. Use a concise, descriptive title.
3. Include a description, goal, acceptance criteria, and technical notes when relevant.

The GitHub Issue is the source of truth for the task. Record its issue number.

## 3. One Task = One Branch

Every issue must be implemented on its own branch. Never implement unrelated backlog items on the same branch.

Branch from the latest `main`:

```text
git checkout main
git pull
```

Use one of these branch naming conventions:

```text
feature/<issue-number>-<short-description>
fix/<issue-number>-<short-description>
refactor/<issue-number>-<short-description>
chore/<issue-number>-<short-description>
docs/<issue-number>-<short-description>
```

Do not work directly on `main`.

## 4. Implementation Scope

Implement only what is required by the associated issue. Do not introduce unrelated features, refactoring, package upgrades, architecture changes, formatting changes, or cleanup.

If additional work is discovered, create or propose a separate backlog item instead of silently expanding the current task. Keep changes small and reviewable.

## 5. Architecture

Respect the existing ServiceDesk architecture and project boundaries:

```text
src/
  ServiceDesk.Domain
  ServiceDesk.UseCases
  ServiceDesk.Infrastructure
  ServiceDesk.WebApi

tests/
  ServiceDesk.Domain.Tests
  ServiceDesk.UseCases.Tests
  ServiceDesk.WebApi.IntegrationTests
```

Do not move responsibilities between layers without an explicit architectural requirement. Do not introduce production dependencies unless they are necessary for the issue. Prefer existing project patterns and conventions.

## 6. Testing

Before committing:

1. Build the solution.
2. Run relevant tests.
3. Run the complete test suite when practical.
4. Verify that existing functionality has not been broken.

Typical commands:

```text
dotnet restore
dotnet build
dotnet test
```

New behavior should have appropriate automated tests. Bug fixes should include a regression test whenever practical. Do not claim tests passed unless they were actually executed successfully.

If tests cannot run because of an environmental problem, report the exact reason in the Pull Request.

## 7. Commits

Every commit must belong to the current issue. Commit messages must reference the GitHub Issue.

Preferred format:

```text
<type>: <description> (#<issue-number>)
```

Recommended types are `feat`, `fix`, `test`, `refactor`, `docs`, `chore`, `build`, and `ci`.

Keep commits focused. Do not combine unrelated changes in a commit. Never commit directly to `main`.

## 8. Pull Requests

Every code change must be submitted through a Pull Request. Never merge code directly into `main`.

After implementation:

1. Push the working branch.
2. Create a Pull Request targeting `main`.
3. Link the associated issue.
4. Include `Closes #<issue-number>` when merging the PR should close the issue.

PR title format:

```text
[Issue #<number>] <description>
```

PR descriptions should contain:

```text
## Summary
Brief explanation of what changed.

## Related Issue
Closes #<issue-number>

## Changes
- Important implementation changes
- Tests added or modified
- Relevant architectural decisions

## Validation
- Build: PASS / FAIL
- Tests: PASS / FAIL
- Number of tests executed when available

## Notes
Any limitations, assumptions, environmental problems, or follow-up work.
```

## 9. Approval Is Mandatory

Creating a Pull Request does not authorize merging it. After opening the PR, stop and wait for human review and approval.

If review changes are requested:

1. Modify the existing PR branch.
2. Run the relevant tests again.
3. Commit the changes referencing the same issue.
4. Push the branch.
5. Update the PR.
6. Wait for approval again.

Never interpret silence as approval.

## 10. Main Branch Protection

Treat `main` as protected even if GitHub configuration currently allows direct pushes.

Never:

- Commit directly to `main`.
- Push directly to `main`.
- Force-push `main`.
- Merge without approval.
- Bypass required checks.
- Disable branch protection.
- Modify repository rules to bypass this workflow.

If repository configuration prevents the required workflow, report the problem instead of bypassing it.

## 11. Existing Uncommitted Changes

Before starting work, inspect the repository state with `git status`.

Do not overwrite, delete, commit, stash, or otherwise modify unrelated user changes without explicit permission. If existing changes conflict with the requested task, stop and report the conflict.

## 12. Issue Scope Changes

If requested implementation becomes materially larger than the original issue, stop before expanding scope. Explain the additional work discovered and recommend creating another backlog item. Do not hide additional work inside the current PR.

## 13. Definition of Done

A task is implementation-complete only when:

- A GitHub Issue exists.
- A dedicated branch exists.
- Implementation matches the acceptance criteria.
- Relevant tests exist.
- Build has been attempted.
- Tests have been attempted.
- Commits reference the issue.
- Branch has been pushed.
- A Pull Request exists.
- The PR references or closes the issue.
- CI/check status is reported.

The task is not merged or fully closed until human approval and PR merge occur.

## 14. Final Response

After creating the Pull Request, report:

- GitHub Issue number and title.
- Branch name.
- Commit(s).
- Build result.
- Test result.
- Pull Request number and link.
- Any warnings or unresolved issues.

Then stop and wait for review. Do not merge the Pull Request automatically.

## 15. Exceptions

These workflow rules may only be bypassed when the user explicitly instructs a specific exception. Do not infer exceptions.

When uncertain, preserve the safer workflow:

```text
Issue → Branch → Code → Tests → Commit → PR → Human Approval → Merge
```
