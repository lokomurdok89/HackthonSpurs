# Code Review Guide

Review findings before summaries. Ground each finding in evidence and prioritize
behavior, security and operational impact over style.

## Checklist

- Scope is intentional and unrelated behavior is unchanged.
- Architecture and ownership boundaries remain coherent.
- Public behavior, errors and compatibility are understood.
- Authentication, authorization, input handling, data and secrets are reviewed.
- Tests match the risk and changed behavior.
- Configuration, dependencies and observability are appropriate.
- Documentation, migrations and operational impact are addressed.

State assumptions, validation gaps and residual risks. If no issue is found,
say so clearly without implying full certification.
