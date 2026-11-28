# Optional SPUR page reader

The private `spur-reader` is the only process that makes the external OCR call. This service has **no database connection, originals volume or published port**. Optional [semantic indexing and search](semantic-search.md) use a separate, bounded embeddings endpoint. The default stack and the synthetic Worker do not call it and do not require a key. With the overlay enabled, a dedicated Worker claims only jobs submitted for SPUR. Do not send challenge documents until the owner has approved the provider's processing and retention terms.

## Environment

Set these in the Git-ignored root `.env` for the optional Compose overlay. Do not add them to Vite, client requests, logs or screenshots. The API and optional index worker share the private key for embedding requests, but they do not call vision OCR.

| Variable | Value |
|---|---|
| `SPUR_OCR_ENDPOINT` | `https://ai.spuric.com/v1/chat/completions`. Required; only the official HTTPS destination/path is accepted. Redirects are disabled. |
| `SPUR_OCR_MODEL` | `spur-vision` for **Qwen2.5-VL 7B**, as mapped in [SPUR's model catalog](https://ai.spuric.com/models). Passed verbatim; no fallback to the text model in the generic curl example. |
| `SPUR_API_KEY` | Private hackathon API key. Required; leave the example empty. Used only in the per-request Bearer header. |

With the root Compose settings from the [local runbook](../README.md#run-locally) configured, start the API, reader, document worker and optional semantic index worker:

```sh
docker compose -f compose.yaml -f compose.spur.yaml up --build -d api spur-reader spur-worker spur-index-worker
```

Startup does not submit any files. If previously submitted OCR jobs are already queued, the Worker will resume them. Previously queued index jobs also resume; completed readings with no job are not backfilled. For OCR without embeddings, set `SPUR_EMBEDDINGS_ENABLED=false` and omit `spur-index-worker`. Missing required configuration fails closed. Never print the resolved Compose environment with a real key. For Docker hosts requiring a subnet override, include the repository's `compose.subnet.yaml` as an additional file and use the configured local subnet.

## OCR preparation limits

The optional Worker leaves fitting images unchanged. It renders PDFs as PNGs and
prepares every page before sending the first one. Each image must fit both
`SPUR_OCR_IMAGE_TARGET_BYTES` (inclusive maximum 79,999 bytes) and
`SPUR_OCR_MAX_JSON_BYTES` (default 128,000 bytes for the exact UTF-8 envelope).
After lossless optimization, oversized opaque grayscale pages may be converted
to a 1-bit PNG with Magick.NET (25×25 adaptive threshold, offset −28).
This derivative can lose faint text. Set `SPUR_OCR_ADAPTIVE_MONOCHROME=false`
to disable it. Color pages and over-budget or blank candidates fall back to
lossless horizontal bands of the source raster. If no safe cut fits, preflight
fails locally. Partial readings are not published. Originals remain untouched.

## Send a document to OCR

Start the frontend with `npm --prefix frontend run dev:api` and open [Documents](http://127.0.0.1:5173/#/documents). Select a file and click **Send to OCR**. There is no checkbox; selecting a file sends nothing. **Upload document locally** remains available for PDF/XLSX/CSV without sending anything to SPUR.

| Original | What SPUR receives |
|---|---|
| PNG or JPEG (up to 4 MiB) | Page 1, unchanged if it fits; otherwise prepared as above. |
| PDF (including scans) | One PNG per page or ordered bands, at the 1800-pixel render scale. |
| Word DOCX, Excel XLSX, PowerPoint PPTX | Printable pages produced by LibreOffice. These are page locators, not worksheet cell references. |
| UTF-8 TXT or CSV | Printed pages of literal text, not evaluated formulas. |

Documents are limited to 20 MiB (or the upload limit if smaller) and 20 printed pages. For legacy, encrypted or unrecognized formats, export an unlocked PDF. Split longer documents before submitting. Hidden sheets, attachments and other non-printable content are not transcribed. Each returned transcript or blank-reading warning is tied to its page. Reopening a receipt does not call SPUR.

The form sets `X-OCR-Provider: spur` for OCR. The database stores that choice separately from the file type. Existing image jobs retain their route; previously queued local documents are not sent. The same key, bytes and mode replay the receipt. Only the optional Worker image contains LibreOffice and Poppler. It verifies the original hash, preflights all pages in temporary storage and sends one image per request. A document has one attempt, a five-minute conversion/reading deadline and a six-minute lease. A later page failure marks the job failed; partial text is not published and already-billed pages are not automatically resent.

## One page per call

From an authorized internal caller on the same Compose network, the request below **will send those image bytes to SPUR and may consume credits**. Use a synthetic test image for an explicitly authorized live check. This is not a browser endpoint.

```sh
curl -X POST http://spur-reader:8080/internal/read \
  -H 'X-Reader-Contract: 1' \
  -H 'X-Document-Id: 00000000-0000-4000-8000-000000000001' \
  -H 'X-Page-Number: 1' -H 'Content-Type: image/png' \
  --data-binary '@synthetic-page.png'
```

The endpoint requires a known length (1 byte to 4 MiB), JPEG/PNG signature and a one-based original page number. It does not fetch URLs, follow links, decode PDFs or accept XLSX/CSV. The document Worker renders the original and supplies its printed page number.

`IPageTranscriber` separates the page contract from the SPUR adapter. The adapter sends one JSON chat completion: the configured model and one user message with a transcription instruction and an inline base64 image (`image_url`). No local ID, path, filename or page number is sent to SPUR. See [SPUR API documentation](https://ai.spuric.com/docs). The adapter expects a completed assistant message, not tool calls or a truncated response. It makes no application-level retries. Its HTTP timeout is 8 seconds; the response is capped at 128 KiB.

The reader returns technical evidence with `method: ocr`, the caller's page locator and a reader version identifying the configured model. Empty text yields a located warning, not an origin claim. Credential/request rejection and invalid payloads return safe 422 codes; rate limits, network errors or upstream outages return 503; timeout returns 504. Raw provider errors and headers are not returned.

## Offline verification

```sh
dotnet run -c Release --project tests/spur-reader/SpurReader.Tests.csproj
```

Uses an in-memory HTTP handler and an invented key; it does not load your root `.env` or contact SPUR. Passing this test does not establish account access, latency, OCR accuracy, or permission to send private files.
