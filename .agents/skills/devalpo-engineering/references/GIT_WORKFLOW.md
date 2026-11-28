# Git Workflow

- Work on small, purpose-named branches.
- Make clear, focused commits that can be reviewed or reverted independently.
- Use pull requests for shared changes; review scope, validation and risk before
  merge.
- Do not merge automatically or rewrite shared history without explicit
  authorization.
- Run the relevant build, tests and checks before requesting review, or report
  what was not run and why.
- Review migration and rollback implications for database changes.
- Never commit or push secrets, credentials or client data.
- Delete merged branches only after confirming they are no longer needed and
  preserve any required release or audit references.
