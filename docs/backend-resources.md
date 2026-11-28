# Backend resources and error dictionary

Edit [backend-resources.json](../backend/HackthonSpurs.Resources/backend-resources.json). It is the **single authored source** for application errors, safe English messages, configuration names, non-secret defaults, bounds, durations, service locations, wire constants and renderer settings. The five .NET projects consume a small shared assembly with no third-party packages. No resource file needs a runtime working directory or network lookup.

## Where a value belongs

| Value | Where to change it |
|---|---|
| Failure or warning | `Errors` entry: stable wire `code`, English `message`, optional HTTP `status`, optional user-facing `uiMessage`. A job-only code does not imply an HTTP status. |
| Error returned by the private OCR reader | Mark `reader: true` and specify the **reader's** status, not the provider's raw status. The Worker accepts only the matching code/status pair. |
| Environment key, default, hard limit or timeout | `SettingNames`, `ServiceDefaults` or `Policy`. Existing runtime configuration still overrides configurable defaults; fixed safety limits are not new operator switches. |
| Service protocol, file format, conversion or log text | `Wire`, `WireFields`, `Media`, `FileFormat`, `RenderSettings` or `ResourceText`. Log templates must not contain document text, credentials or raw provider diagnostics. |
| Actual credential, connection string or deployment-specific override | Private environment/secret store or existing host configuration. **Never** add the value to the catalog. |

SQL and EF schema definitions, historical migrations, byte signatures, standard format specifiers, collection offsets and independent test expectations are not operator settings. Keep these structural literals with their owner. If a policy change affects a database constraint or public API, review the migration and contract separately; editing the catalog does not migrate a database.

## Diagnose a failed receipt

| Stored code | Meaning |
|---|---|
| `reader_credentials_rejected` | The gateway rejected server credentials; check server-side configuration, not the PDF. |
| `reader_request_rejected` | The gateway declined the page request. This is distinct from an authentication failure. |
| `invalid_reader_result` | An invalid or incomplete response was rejected; no partial transcript is published. |
| `reader_timeout` / `reader_unavailable` | The reader timed out or was unavailable. The resource change does not authorize a retry. |
| `reader_rejected` | No trusted, more specific cause is available. Do not infer the cause of an older receipt. |

The private reader returns only safe codes. The Worker checks the response type, size, status and an allowlist; unknown, duplicate, mismatched and malformed codes cannot enter a new receipt. Transient HTTP failures still use the existing retry policy. The Documents UI uses only the generated **public message subset**, never the server configuration. There is no new public endpoint or change to the receipt schema. Existing failed receipts are neither rewritten nor resubmitted.

## Make and verify a change

1. Update the canonical JSON. Do not edit generated .NET or TypeScript bindings.
2. Run `python -B tools/generate_backend_resources.py` at the Git root. Python uses only the standard library; a normal .NET build uses the checked-in binding and does not require Python.
3. Run `pwsh -NoProfile -File tests/quality/Test-BackendResources.ps1`. It checks generated files, unique stable codes, inline C# string literals and direct numeric timeout/config defaults. Also run the class-size and OpenAPI checks.
4. Run the offline Worker and private reader harnesses and `python -B tests/integration/test_image_transcript.py`. The browser suite uses synthetic documents and a fake reader to check that a known cause survives all the way to the screen without private details.

## Verification snapshot (local working tree, 2026-11-27)

The resource check passed with 42 unique error/warning entries. Release build, both offline .NET harnesses, 50 OpenAPI response examples, frontend lint/build and the 200-line checks passed. Disposable integration runs: API 5/5, Worker 5/5, OCR/UI 8/8, semantic search 7/7 (browser suites share inherited cases). Empty Compose overrides were also tested with a synthetic credential and empty database; the API started with the catalog defaults and made no provider request.

The local SonarQube gate against the existing project baseline **did not pass**: new-code coverage 25.2% (80% required), one new issue (zero required), 0.0% duplication. The scan did not import browser LCOV or container coverage from sources whose hashes changed. Moving allowlisted internal HTTP destinations into resources does not encrypt that traffic. Do not infer release approval from the passing functional checks.
