# Testing Standards

Choose testing depth according to risk, criticality, change type and the
existing system. Do not use a universal coverage percentage as a quality proxy.

| Test type | Purpose |
| --- | --- |
| Unit | Isolate rules, parsing, transformations and error cases. |
| Integration | Verify collaboration with real adapters or bounded dependencies. |
| API or component | Verify public behavior, authentication and composition. |
| Persistence | Verify mappings, migrations and queries against a representative database. |
| Smoke | Verify a deployed or configured dependency is reachable. |
| End-to-end | Verify an important user or system journey across boundaries. |
| Manual | Capture exploratory, visual or environment-dependent evidence. |

Run the smallest meaningful set for the change. Require stronger persistence,
security or end-to-end evidence when shared contracts, data handling, external
systems or operational behavior changes. A validation may be reported as passed
only when it was actually executed successfully.
