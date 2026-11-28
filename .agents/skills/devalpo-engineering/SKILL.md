---
name: devalpo-engineering
description: Apply DeValpo engineering standards when reviewing a HackthonSpurs change, choosing validation checks, or drafting project context, architecture notes, ADRs, testing strategies, or engineering backlog items.
---

# DeValpo engineering

Start with the requested outcome, the repository instructions, and the current
contract or implementation. Use the DeValpo guides as defaults: a documented
repository or module exception takes precedence. Identify the exception and
its reason rather than treating it as a defect. Do not turn a documentation
or review request into an implementation, a refactor, or a release.

| When asked to… | Use these references |
| --- | --- |
| Adopt standards or document project scope | [Engineering handbook](references/ENGINEERING_HANDBOOK.md); start with existing canonical documents, not a second set. |
| Assess an architecture decision or implementation | [Architecture principles](references/ARCHITECTURE_PRINCIPLES.md) and [development standards](references/DEVELOPMENT_STANDARDS.md). Identify a concrete trade-off; the baseline does not mandate a framework. |
| Review a change | [Code review guide](references/CODE_REVIEW_GUIDE.md). Lead with findings, evidence and impact. For uploads, permissions or sensitive data, also consult the [security baseline](references/SECURITY_BASELINE.md); distinguish expected controls from verified ones. |
| Plan tests or assess readiness | [Testing standards](references/TESTING_STANDARDS.md) and [Definition of Done](references/DEFINITION_OF_DONE.md). Select checks that exercise the changed behavior. For example, an HTTP contract check alone does not verify a persistence migration. Report checks as passed only if run. |
| Prepare work for a shared branch | [Git workflow](references/GIT_WORKFLOW.md). A review or readiness assessment does not authorize a commit, push, merge or deployment. |

## Templates

Use only the template the task needs. If a canonical document already exists,
update or reference it instead of generating a parallel one.

- Repository onboarding: [agent guide](assets/templates/AGENTS.template.md) or [project context](assets/templates/PROJECT_CONTEXT.template.md).
- Design: [architecture note](assets/templates/ARCHITECTURE.template.md) for boundaries; [ADR](assets/templates/ADR.template.md) for a decision and its consequences.
- Validation and follow-up: [testing strategy](assets/templates/TESTING_STRATEGY.template.md) or [engineering backlog item](assets/templates/ENGINEERING_BACKLOG.template.md).

## Provenance

The eight references and six templates are bundled from `DeValpo.Engineering`
at commit `09c3d635f1ba79292d8a1d0798bea7a0b8753f64`. No separate checkout
is needed. When refreshing this snapshot, compare against a specific upstream
revision, review changes to the guides and templates, and update the pin.
