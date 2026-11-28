# Engineering Handbook

## Purpose

This repository provides reusable default standards, templates and review
practices for DeValpo-owned projects. It does not replace project knowledge,
product decisions or applicable legal and operational obligations.

## Instruction Hierarchy

Apply instructions in this order:

1. Personal global instructions.
2. DeValpo engineering standards.
3. Repository-specific rules.
4. Module or subdirectory-specific rules.

When a documented contradiction exists, the more specific instruction prevails.
Record a material exception close to the affected repository or module, with its
reason and review date.

## Starting A New Project

1. Define the purpose, owners, supported environments and sensitive-data scope.
2. Add a short repository `AGENTS.md` and a project context using the templates.
3. Choose only the standards and templates that fit the technology and risk.
4. Define build, test, security and delivery checks before broad implementation.
5. Record significant long-lived choices as ADRs.

## Adapting An Existing Project

Start with discovery: inspect existing architecture, conventions, tests,
automation and documentation. Adopt standards incrementally, preserve working
contracts and document exceptions. Do not apply mass refactors or mass policy
changes without review.

## Documentation Discipline

Use one canonical document for each durable topic and link to it from shorter
guides. Keep verified facts, decisions, assumptions, risks and pending work
separate. Update documentation when behavior, architecture, configuration or
operating responsibility changes.

## AI Agents

Agents must read repository instructions before editing, preserve scope, avoid
secrets and report executed validations honestly. They may use these standards
as defaults, but must follow the consumer repository's documented overrides.

## Required And Optional Documents

Required for a maintained project: repository instructions, project context,
development/testing guidance, security expectations and a Definition of Done.
Use architecture, ADR, backlog, operational and API documents when the project
has corresponding complexity or risk.
