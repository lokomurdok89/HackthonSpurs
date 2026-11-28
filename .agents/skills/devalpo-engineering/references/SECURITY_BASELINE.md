# Security Baseline

This baseline identifies controls to evaluate; it does not assert that every
consumer project has implemented them.

- Keep secrets out of source, logs, examples and tickets; use approved secret
  management and rotation appropriate to the environment.
- Design authentication and authorization around explicit identities, scopes and
  least privilege.
- Validate untrusted input at boundaries, including uploaded files, URLs,
  webhooks and provider responses.
- Classify sensitive data and minimize its collection, storage, exposure and
  retention.
- Use structured logs that support diagnosis without recording secrets or
  sensitive payloads.
- Review dependency provenance, updates and known vulnerabilities.
- Apply upload controls such as size/type validation, storage boundaries and,
  where needed, malware scanning.
- Treat outbound webhooks and URLs as SSRF risks; constrain destinations,
  protocols, redirects and network ranges.
- Apply rate limiting, timeouts and resource limits to exposed operations.
- Use encryption in transit and at rest where risk and data classification
  require it.
- Keep environment configuration separate and review security risks before each
  deployment.

Record accepted risks, compensating controls and owners in the consumer
repository rather than assuming this baseline is sufficient by itself.
