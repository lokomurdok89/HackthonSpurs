# HackthonSpurs engineering rules

- Read the [DeValpo engineering skill](.agents/skills/devalpo-engineering/SKILL.md), [maintained architecture](docs/ARCHITECTURE.md) and affected contracts before editing.
- Keep each class at **200 physical lines or fewer**, including blank lines and comments between the declaration and closing brace. Count C# partial declarations together, including committed generated parts. Split by responsibility, not into arbitrary partials.
- Run `pwsh -NoProfile -File tests/quality/Test-ClassSize.ps1` at the Git root. For TypeScript and React, `npm --prefix frontend run lint` uses a stricter 200-line limit per source file, also covering function components. Python test modules use the same conservative file limit.
- Write project documentation in English. Use only synthetic fixtures in version control and tests; never publish local demo data, uploads or secrets.
