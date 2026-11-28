# OCR to pgvector: local integration review

**Decision (2026-11-27): verified for a single-user local demo with synthetic documents. Not approved for merge or a shared deployment: the SonarQube quality gate failed.** This is a review of an uncommitted working tree on `feat/spur-ocr-adapter`, not a CI result for a published revision. The reference baseline was `origin/main` at `b935400`.

## Observed behavior

A single **Send to OCR** action creates a receipt. OCR publishes its transcript and schedules indexing in the same PostgreSQL statement. The index worker reads only that persisted text; it has no originals mount. The browser test uploaded a two-page synthetic PDF once, restarted the API with indexing stopped, and ran two index workers. The search hit for page two opened and focused the correct transcript. Replaying the same upload key did not create a second job. A provider error or incompatible vector left the successful OCR reading available. The expired-lease test rejected stale publication; external embedding calls themselves are not exactly-once.

The optional stack runs on the existing local database volume, PostgreSQL **18.4**, pgvector **0.8.2**. Before updating the live database, a private dump was restored in an isolated container. Rehearsing the migrations preserved the existing document rows and created no historical index work. A fresh cluster also enabled the extension, and a rollback-only SQL check verified cosine distance and nearest-neighbor ordering. The live DB port is not published. Docker Desktop became temporarily unavailable during repeated test runs; after recovery, the team stack, SQL check, live browser navigation and final semantic suite were verified again. No live SPUR inference was performed in this review.

## Checks actually run

Run at the repository root. The integration suites create disposable Compose projects; on hosts without a free Docker default subnet, supply the repository subnet override and an unused test subnet. The browser runs used Chrome (`SPURS_BROWSER_CHANNEL=chrome`).

| Check | Command or method | Observed |
|---|---|---|
| .NET 10 | `dotnet restore backend/HackthonSpurs.slnx --configfile backend/NuGet.Config`; `dotnet build backend/HackthonSpurs.slnx -c Release --no-restore` | 0 errors, 0 warnings |
| Worker and SPUR reader | `dotnet tests/worker/bin/Release/net10.0/WorkerContract.Tests.dll`; `dotnet tests/spur-reader/bin/Release/net10.0/SpurReader.Tests.dll` | Passed; includes page boundaries, batch order, invalid vectors, safe provider errors and disabled-client behavior |
| Public contract | `pwsh -NoProfile -File tests/contract/Test-DocumentContract.ps1` | 50 response examples, 6 routes |
| Database model | `dotnet-ef migrations has-pending-model-changes --project backend/HackthonSpurs.Documents --startup-project backend/HackthonSpurs.Api --configuration Release --no-build` | No pending EF model change |
| Actual pgvector | `pwsh -NoProfile -File tests/quality/Test-Pgvector.ps1` | Passed on the fresh test cluster and active demo database |
| API / original / idempotency | `python -B tests/integration/test_document_api.py` | 5 passed |
| Worker / lease / retries | `python -B tests/integration/test_document_worker.py` | 5 passed |
| OCR formats and transcript UI | `python -B tests/integration/test_image_transcript.py` | 7 passed |
| Semantic search, failure isolation and page link | `python -B tests/integration/test_semantic_index.py` | 7 passed on final independent run. Browser suites share some inherited cases. |
| Frontend, size and whitespace | `npm --prefix frontend run lint`; `npm --prefix frontend run build`; `pwsh -NoProfile -File tests/quality/Test-ClassSize.ps1`; `git diff --check` | Passed; 75 C# types / Python modules, plus TypeScript lint |

### SonarQube: gate failed

The local private SonarQube scan compared first-party source snapshots of the baseline and working tree using SonarScanner for .NET (`begin`, Release build, `end`). It analyzed C# and the frontend; it excluded migrations, generated build outputs and dependencies. Microsoft coverage came from the offline .NET harnesses and attributable container integration hits, mapped back to matching sources by SHA-256. The final scan imported two coverage reports. No frontend LCOV report was supplied. The scanner treated console harness sources as main code. This is the instrumented coverage reported by the gate, not a count of browser scenarios or a claim of full-stack coverage. No issue was suppressed and no gate threshold was lowered.

| New-code condition | Required | Observed |
|---|---:|---:|
| Coverage | ≥80% | **37.5% — fail** |
| New issues | 0 | **4 — fail** |
| Duplicated lines | ≤3% | **0.0% — pass** |

All four open new findings are `S5332` (HTTP): the two private Worker-to-reader destinations, the testing-only embedding stand-in, and an OCR test that explicitly rejects an HTTP URL. The real provider destination is HTTPS. **Before merge:** disposition each finding with evidence (test-only URL versus real internal transport); do not grant a blanket exception. Add frontend coverage reporting and align test classification and remaining backend coverage with the team's baseline, then rerun the unmodified gate. This review did not establish provider availability, billing, transcription accuracy, or an origin/compliance claim.

See the [semantic-search runbook](semantic-search.md), [database runbook](pgvector-local.md), and [public contract](api/openapi.json).
