# Synthetic Worker and private reader contract

**Purpose:** prove the upload → claim → HTTP reader → PostgreSQL → browser path. This profile exercises real infrastructure, but it does not read arbitrary documents or invoke OCR. The only eligible input is the [unchanged synthetic CSV](../backend/Testing/HackthonSpurs.Reader.Fixture/worker-demo.csv). See the [local runbook](../README.md#run-the-synthetic-worker-demo).

## Internal HTTP v1

| Item | Contract for the fixture adapter |
|---|---|
| Request | `POST /internal/read`, raw file bytes (not multipart), `Content-Type: text/csv`, explicit length, `X-Document-Id: <uuid>`, `X-Reader-Contract: 1`. No filename, filesystem path, credentials or callback URL. |
| Response | HTTP 200 and JSON conforming to the public [ReadResult](api/openapi.json). Version `1`, matching ID, reader `synthetic-fixture/1`, one known native CSV fragment at row 2, column 1 and no warnings. This is a strict subset, **not** the hackathon provider's schema. |
| Bounds | Select by fixture SHA-256 and media type; verify stored byte count and hash before sending any bytes. The fixture reader independently checks its embedded bytes and limits the body to 1 KiB. The Worker uses a fixed internal destination, no redirects or proxy, a 10 s HTTP timeout and a 64 KiB response bound. |
| Error | Timeout, network failure, HTTP 408/429/5xx and temporarily inaccessible originals are retryable. A changed original, malformed response, mismatched identity/locator or other rejection does not publish fragments. Persist safe codes, not upstream messages. |
| Isolation | Compose profile `synthetic` only. The reader has no published port, SQL credentials or originals mount. The Worker mounts originals read-only. The default API-only stack is unchanged. |

## Durable ownership

One conditional PostgreSQL statement uses `FOR UPDATE SKIP LOCKED`, increments the attempt and grants a fresh 30 s lease before the network call. One Worker handles one job at a time. A matching token **and** an unexpired lease are required to atomically settle the job: publish JSON and `Completed`, requeue with a database timestamp, or mark `Failed` without a result. On a transient failure the first two attempts schedule delays of 5 s and 30 s. Three claims total. An expired third claim is failed rather than claimed a fourth time. Idle polling is once per second.

Queue and result writes are separate from HTTP execution: a crash can repeat reading, not acceptance or publication by an obsolete owner. Only the API applies the additive EF migration. To revert the demo operationally, stop the profile and use the plain API; there is no need to drop data or roll back the schema. No original files or existing results are rewritten by migration.

## Before connecting the hackathon OCR

The separate [SPUR adapter](spur-ocr.md) now implements the page-image transport; it does not change fixture eligibility. A separate document Worker renders uploads submitted with Send to OCR. Before enabling automated processing, obtain the event-specific provider guidance (no secrets in Git). Confirm runtime/location, authorization to transfer files, retention, authentication, supported formats, request and result limits, locators, warning semantics, sync/async operation and timeout behavior. Keep credentials on the reader side. Replace the fixture-only eligibility and validator deliberately; do not just change the URL or treat synthetic results as provider evidence. Google OCR is not part of this plan.

## Checks

- `dotnet run --project tests/worker/WorkerContract.Tests.csproj -c Release`: invalid responses rejected, unrecognized properties never published.
- `python -B tests/integration/test_document_worker.py`: real queue and reader, crash/reclaim, stale response fencing, retry limit, private-file integrity, exact-byte eligibility and replay.
- `python -B tests/integration/test_document_ui.py`: the browser uploads the fixture and displays its persisted result after refresh. A separate, seeded case tests warning presentation. Neither proves real extraction or origin.
