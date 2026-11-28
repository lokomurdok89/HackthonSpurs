# Diagnose an existing document receipt

Use an authorized workspace account. The **Reader** role is sufficient; no
administrator credentials are needed. From the repository root, replace the
synthetic UUID below with the receipt ID and verify the port is the running
local workspace API before entering any credentials.

```sh
python -B tools/diagnose_receipt.py 10000000-0000-4000-8000-000000000002 --port 5080 --login --with-index
```

`--login` prompts for the username and password without echo. If a no-echo
terminal is unavailable, it refuses to prompt. Use `--login-stdin` instead only
when a trusted local automation process can provide one private JSON document
with string fields `username` and `password` through a pipe, then close it. Do
not put credentials in command arguments, environment variables, inline shell
commands, files or shell history.

Only the authentication session is created and revoked by the command. It signs
in at `POST /api/auth/login`, keeps the returned workspace cookie in memory for
the selected status GETs and attempts to revoke it with `POST /api/auth/logout`.
The command only connects directly to `127.0.0.1`; it does not use proxies or
follow redirects. It never opens originals, fetches transcripts or search
results, schedules work or calls external providers.

The default reads only `GET /api/documents/{id}`. Add `--with-index` when the
deployment exposes `GET /api/documents/{id}/index-status`; see the
[published contract](api/openapi.json). The two lookups are separate snapshots.
If index lookup fails, the reading status that was successfully retrieved
remains in the report. A technically completed reading does not prove that
the text is accurate. If reading completed but indexing failed, do not resend
the file.

The JSON report contains only the observed state, attempt count and recognized
failure codes, or safe lookup/session errors. Exit `0` means sign-in, every
requested lookup and sign-out succeeded; it does **not** mean that OCR or
indexing succeeded. Exit `1` means sign-in, a lookup, or sign-out failed; exit
`2` indicates invalid arguments or unavailable/invalid credential input.
HTTP `401` is an authentication/session error. HTTP `404` means the receipt
or requested route was not found. Neither establishes a job failure.

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

## Verification

```sh
python -B -m unittest discover -s tests/quality -p 'test_receipt_diagnostic*.py'
python -B tests/integration/receipt_diagnostics_test.py
```

The first command uses a loopback HTTP mock. The second requires Docker and
Compose and runs the actual CLI against a disposable API and PostgreSQL project.
It creates a temporary synthetic reader and receipt, verifies anonymous requests
are refused, reads both statuses as the reader, and verifies the session was
revoked. It does not run the receipt through OCR or contact SPUR.
If automatic Docker address pools are exhausted, set `COMPOSE_FILE` to
`compose.yaml` plus `compose.subnet.yaml` (using the host path-list separator)
and set `SPURS_SUBNET` to a verified unused private subnet.
