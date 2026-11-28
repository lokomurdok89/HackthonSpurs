# Document API · UI handoff

[OpenAPI contract](openapi.json) · [Architecture and demo walkthrough](../ARCHITECTURE.md)

**Status: locally implemented.** Run the [API and PostgreSQL](../../README.md) or use the invented examples below without any backend. The [Documents screen](../../frontend/README.md) supports both modes and labels them separately. Only the [optional synthetic Worker profile](../../README.md#run-the-synthetic-worker-demo) publishes a reading for the exact demo CSV. The optional [SPUR overlay](../spur-ocr.md) renders PDF/Office/text documents for OCR and accepts PNG/JPEG directly; the same receipt and result routes return the transcription. Other live uploads remain queued. No public deployment or access to the event dataset is required. The OpenAPI file is the source of truth for field names, casing, media types, examples and errors.

## Upload → poll → read

| Request | What the UI should do |
|---|---|
| `POST /api/documents` | Send exactly one `file` part and an `Idempotency-Key` UUID. Store the returned `id` and relative `Location` after `202`. Upload the workbook and certificate separately. Acceptance means the original and queued job are durable, not that reading has finished. |
| `GET /api/documents/{id}` | Poll while `state` is `Queued` or `Processing`. A retry can move back to `Queued`; skipped states between polls are normal. Start at 2 s, back off on connection problems or `503`, cap at 10 s and pause when the view is closed. Resume from the saved ID, not by uploading again. Stop on `Completed` or `Failed`. |
| `GET /api/documents/{id}/result` | After `Completed`, render the persisted fragments and located warnings. After `Failed`, display a safe message based on `failureCode`, not an empty result. GETs have no processing side effects. |

**Browser upload example** (the caller creates and retains the key before its first attempt):

```ts
async function upload(file: File, operationKey: string) {
  const data = new FormData();
  data.append("file", file);
  return fetch("/api/documents", {
    method: "POST",
    headers: { "Idempotency-Key": operationKey },
    body: data,
  });
}
```

Use `crypto.randomUUID()` per intentional file submission. Let the browser set the multipart boundary. If the response is lost, retry the **same bytes and key**; the same receipt is returned even if processing already finished. For a changed file or a deliberate new submission, use a new key. Handle `400` (input), `413` (file too large) and `415` (unsupported type) without retrying the unchanged request. The initial contract covers PDF, XLSX and CSV, with a proposed configurable default of 20 MiB per file.

**Do not treat errors as empty evidence.** Errors use `application/problem+json`; branch on the stable `code`, not the English `title`. Upload `409/idempotency_conflict` means the key belongs to different bytes. On result retrieval, `409/result_not_ready` means return to status polling; `409/processing_failed` is terminal. `404/document_not_found` means the ID is unknown, not queued. Never display or log raw internal failures.

## Index → search (optional SPUR overlay)

| Request | What the UI should do |
|---|---|
| `GET /api/documents/{id}/index-status` | Poll `AwaitingReading`, `Queued` or `Processing` separately from OCR. A completed OCR result is readable immediately. `Skipped` means no readable text; `NotApplicable` is not eligible; `NotIndexed` means the earlier result has no index job. `Completed` and `Failed` are terminal for indexing. |
| `GET /api/search/stats` | Read whether embeddings are configured and how many excerpts and documents match the active profile. This never sends text to SPUR. |
| `POST /api/search` | Explicitly submit JSON `{ "query": "demo certificate", "topK": 8, "documentId": "<receipt UUID>" }`. Query length is 1–1000 after trimming; `topK` is optional (1–50) and the receipt scope may be omitted. When compatible results are available, it sends only the query text to SPUR. No document is created or reindexed by this request. |

There is no public `POST /api/documents/{id}/index` operation and no client callback. The OCR worker schedules indexing atomically when publishing; public status and reading contracts are unchanged. For PR #7 search clients, `locator` stays a display string and `sourceName` stays nullable (always null for new indexes); use the new structured `pageLocator` to open the matching page on the receipt. Keep locator strings and excerpt content as plain text. Provider rejection or downtime is a problem response, not an empty result. See the [semantic search runbook](../semantic-search.md) for model and privacy settings.

## Build against the synthetic examples

Import the OpenAPI file into your local mock tool, or read the named example bodies directly in UI tests. No new runtime dependency is required. From the repository root, this command prints the exact payload for the cropped PDF:

```sh
node -p "JSON.stringify(require('./docs/api/openapi.json').paths['/api/documents/{id}/result'].get.responses['200'].content['application/json'].examples.unreadableSection.value,null,2)"
```

For an HTTP mock, if [Prism](https://github.com/stoplightio/prism/blob/master/docs/guides/01-mocking.md) is already available, run `prism mock docs/api/openapi.json` locally. On a status or result GET, choose a named example using the mock-only header `Prefer: code=200, example=unreadableSection` (use `example=processing` for status). Static examples do not advance time, honor upload keys, or substitute the request ID into responses. Select each case explicitly; use example IDs when testing correlations. Do not send this header to the real API or upload real documents to mocks.

| Workbook example + certificate example | Expected comparison (human review, not a backend verdict) |
|---|---|
| `materialsRow` + `unconfirmedLink` | Only the supplier matches. Show “Link unconfirmed”; do not attribute the PDF to the part. |
| `materialsRow` + `originNotDemonstrated` | Part and certificate IDs match. In this complete, legible sample the PDF gives a grade and supplier address, not a declaration of origin. Do not use the address as origin. |
| `materialsRow` + `unreadableSection` | The IDs still match. Show the page-1 reading warning and invite inspection of the original. Do not claim the origin statement is absent. |

`Materiales` is the synthetic worksheet's literal name, not UI copy. Display original page numbers or worksheet names and A1 ranges; CSV rows and columns are one-based. Render text as untrusted plain text. A technical `Completed` may have warnings and no usable fragments. Never infer an evidence verdict from an empty warnings list. Original-file preview/download and an internal Worker/reader contract are not exposed by the public API; locators are not public file URLs.

## Verification and delivery boundary

Run `pwsh -NoProfile -File tests/contract/Test-DocumentContract.ps1` from the repository root (PowerShell 7.6). It checks the named response examples against their schemas offline, including negative cases. Run `python -B tests/integration/test_document_api.py` to exercise real HTTP and PostgreSQL: durable acceptance, concurrent replay, persisted state/result reads and terminal errors. Worker transitions and result payloads are seeded privately in the test database; these API-only tests do not certify the Worker. The separate Worker and browser suites run the private fixture reader and verify publication without seeding the successful result.

The [backend error dictionary](../backend-resources.md) defines safe problem and job codes. `failureCode` may carry a vetted private reader cause; never display raw provider diagnostics.
