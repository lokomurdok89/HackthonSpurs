# Development Standards

- Follow the existing repository's conventions before introducing a new style.
- Use clear names, small cohesive files and explicit ownership boundaries.
- Keep configuration and user-facing text outside code when the platform and
  project conventions support it.
- Handle expected errors deliberately and log structured diagnostic context.
- Never place secrets, personal data or protected business data in logs.
- Add dependencies only when justified by a concrete need and validate their
  maintenance and security implications.
- Preserve compatibility unless a breaking change is authorized and documented.
- Treat published database migrations as immutable; create follow-up migrations
  rather than rewriting shared history.
- Update affected documentation with the implemented behavior.
- Prefer small, reviewable changes with focused validation.
