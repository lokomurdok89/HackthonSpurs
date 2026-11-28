# Definition Of Done

Apply the sections relevant to the task; do not require cloud end-to-end tests
for trivial or documentation-only work.

## Always Applicable

- Requested scope is complete and exclusions are respected.
- Risks, assumptions and unexecuted validations are reported.
- Secrets and sensitive data are not introduced.
- `git diff --check` and final Git status are reviewed.

## Code

- Architecture, compatibility, errors and logging were considered.
- Appropriate tests were added or updated and relevant checks were run.

## Database

- Model, migration, data-impact and rollback implications were reviewed.

## APIs

- Contract compatibility, authentication, authorization and client impact were reviewed.

## Infrastructure

- Configuration, permissions, deployment safety, observability and rollback were reviewed.

## Documentation

- Canonical documents and templates reflect relevant behavior or decisions.
- Links and documentation build checks were run when available.
