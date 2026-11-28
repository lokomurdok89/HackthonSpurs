# Diagnose an existing document receipt

From the repository root, replace the synthetic UUID below with the receipt ID.
The default reads only the status route in the [published contract](api/openapi.json).

```sh
python -B tools/diagnose_receipt.py 10000000-0000-4000-8000-000000000002 --port 5080
```

If your deployment also exposes `GET /api/documents/{id}/index-status`, add
`--with-index` to read both lanes. This PR does not add that endpoint or a reader.
The two lookups are separate snapshots; an index lookup failure does not invalidate
a successful reading. A technically completed reading does not prove that the
text is accurate. If reading completed but indexing failed, do not resend the file.

The command accepts only a UUID and a loopback API port. It does not follow
redirects, read files or transcripts, schedule work, or call external providers.
It prints the observed state, attempt count and only recognized failure codes.
Exit 0 means every requested lookup succeeded (even if a job failed); exit 1
means at least one lookup failed; exit 2 indicates invalid arguments. HTTP 404
means the receipt or requested route was not found, not that processing failed.

## Reading a failure code

The table includes codes from the extended OCR/index deployment. Not every
deployment emits every code. A stage narrows the investigation; it does not
recover provider details, the failing page or the number of calls.

| Lane and reported stage| Stored code(s) | Evidence and limit |
|---|---|---|
| Reading: `original_verification` | `original_mismatch`, `original_unavailable` | The Worker could not open, read or verify the stored original for this attempt. |
| Reading: `render_preflight` | `unsupported_document`, `too_many_pages` | The Worker declined the input type or printed page count before sending any page for this attempt. |
| Reading: `render_tool` | `renderer_unavailable` | The conversion tool could not be started. For PDF, preparation uses Poppler; the receipt does not identify a specific executable or underlying cause. |
| Reading: `render_or_scratch_io` | `document_not_renderable` | Covers PDF inspection, rasterization, generated-page checks and scratch I/O (including read or cleanup). Does not prove a corrupt PDF, a Poppler error, or that no provider request occurred. |
| Reading: `page_reader_input` | `invalid_reader_request`, `unsupported_image_type`, `length_required`, `image_too_large`, `empty_image`, `incomplete_image` | The private reader rejected the request or page payload. Does not establish that the original PDF is invalid. |
| Reading: `ocr_gateway` | `reader_credentials_rejected`, `reader_request_rejected` | The gateway rejected server credentials or declined a page request, respectively. The stored code does not give the gateway's body or underlying reason. |
| Reading: `reader_or_gateway` | `reader_timeout`, `reader_unavailable` | Timeout or unavailability on the reading request path. The code cannot distinguish local reader transport from the upstream gateway or confirm billing. |
| Reading: `reading_validation` | `invalid_reader_result` | The provider response, private reader result, or combined reading failed validation. No partial transcript is published. |
| Reading: `unknown` | `reader_rejected`, `document_timeout`, `lease_expired` | Generic reader rejection preserves no trusted cause; the document deadline spans preparation and reading; a lease expiry does not identify where execution stopped. |
| Index: `index_input` | `index_source_invalid` | The stored reading could not be used as input to indexing. |
| Index: `index_embedding` | `embedding_provider_timeout`, `embedding_provider_unavailable`, `embedding_provider_rejected`, `embedding_response_invalid` | The indexing lane recorded an embedding request, deadline or validation problem. It does not alter the reading. |
| Index: `unknown` | `index_lease_expired` | The final index lease expired; the code does not identify where the attempt stopped. |

If the code is missing or not recognized for its lane, the tool prints
`failureCode: null` and `failureStage: "unknown"` rather than echoing an
untrusted value. For an unknown historical failure, retain that uncertainty.
Checking status does not authorize or perform a retry.

## SPUR-enabled local deployments only

If new receipts report `reader_unavailable`, check whether the private reader
is running. If its startup check rejects `SPUR_OCR_MODEL`, use the gateway
identifier `spur-vision`, not the display name `Qwen2.5-VL 7B`. Recreate the
reader with the same Compose project and overlays to reload local settings.
Never publish the private environment or print its resolved contents.

## Offline test

```sh
python -B tests/quality/test_receipt_diagnostics.py
```
