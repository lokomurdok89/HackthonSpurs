# HackthonSpurs · Document API

Local, single-user API for the Track 2 demo. It accepts PDF, XLSX and CSV uploads, stores the original privately, and queues one durable PostgreSQL job per operation. It serves status and persisted reading results using the [published OpenAPI contract](docs/api/openapi.json). **Default: API and database only.** The optional [synthetic Worker demo](#run-the-synthetic-worker-demo) processes one known CSV. It is not OCR. All other uploads stay queued.

- [Architecture and first demo walkthrough](docs/ARCHITECTURE.md)
- [Diagnose an existing receipt without resubmitting](docs/receipt-diagnostics.md)
- [Run the React UI against the local API](frontend/README.md)
- [Frontend integration guide and synthetic examples](docs/api/README.md)
- [Live API tests against PostgreSQL](tests/integration/test_document_api.py)
- [DeValpo engineering skill](.agents/skills/devalpo-engineering/SKILL.md) — from this repository, ask Codex: `Use $devalpo-engineering to review this change and explain which checks it needs.`

## Run locally

Use Docker with Linux containers and Docker Compose v2. From the repository root:

1. Create a local `.env` from [.env.example](.env.example) **only if one does not exist**. Set `POSTGRES_PASSWORD` to a random, unique local secret. Keep it private and reuse it for the lifetime of the database volume. Do not publish the resolved Compose configuration.
2. Run `docker compose up --build -d` and check `docker compose ps`. PostgreSQL has no host port. The API runs on loopback at `http://127.0.0.1:5080` by default and applies versioned EF migrations before listening. Use a same-origin dev proxy for a separate UI dev server; do not expose this unauthenticated API to the LAN.
3. Stop with `docker compose down`. The database and original-file volumes survive. No challenge files are mounted. On hosts whose Docker address pools are exhausted, set `SPURS_SUBNET` to an **unused private CIDR** and use `docker compose -f compose.yaml -f compose.subnet.yaml up --build -d`. No other project's networks need to change.

### Run the synthetic Worker demo

After configuring the local Compose secret above, run:

```sh
docker compose --profile synthetic up --build -d
npm --prefix frontend ci
npm --prefix frontend run dev:api
```

In **Documents**, check the **Local API** label and upload the [exact demo CSV](backend/reader-fixture/worker-demo.csv) from disk. Do not recreate or resave it: its bytes are allowlisted by SHA-256. Follow the receipt until **Completed · Attempt 1** (the short Processing state may be missed). Expect **synthetic-fixture/1**, **Row 2, column 1**, and **DEMO-ONLY-014**. Reload the receipt to read the same persisted result. A modified copy or any other file stays queued. No result seeding, document comparison or OCR is involved.

Stop this profile with `docker compose --profile synthetic down` (no `-v`); the demo job and result survive. The profile also picks up previously queued copies of the exact fixture. No provider endpoint, key or challenge data is needed. See the [internal contract and recovery notes](docs/reader-fixture.md).

### Try an upload and check its status

From PowerShell, send this **synthetic** CSV without creating a file:

```powershell
$api = 'http://127.0.0.1:5080'  # Adjust if API_PORT was changed.
$key = [guid]::NewGuid().ToString()
$csv = "part,certificate`r`nCAS-014,CERT-027"
$receipt = $csv | curl.exe --silent --show-error --fail-with-body -H "Idempotency-Key: $key" -F "file=@-;filename=sample.csv;type=text/csv" "$api/api/documents" | ConvertFrom-Json
if ($LASTEXITCODE -ne 0 -or !$receipt.id) { throw 'Upload was not accepted.' }
Invoke-RestMethod "$api/api/documents/$($receipt.id)"
```

Follow the relative `Location` with GET. The status starts at `Queued`, attempt 0; appending `/result` returns `409/result_not_ready`. Replay the same file and key to recover the receipt after an interrupted response. Use a new key only for a new operation. A result becomes readable only after atomic publication by a Worker. There is no public result-seeding or original-download endpoint.

## Configuration and storage

| Setting | Owner and meaning |
|---|---|
| `POSTGRES_PASSWORD` | Required Compose secret. Configures the local database and supplies the API password through `PGPASSWORD`; not part of any HTTP payload. |
| `API_PORT` | Local host port, default 5080. |
| `ConnectionStrings__Documents` | Required for the .NET host. Compose supplies the internal host, database and user. For native development, set a private local value via environment/user tooling. |
| `Storage__Root` | Required absolute private directory, writable by the API. Compose mounts the originals volume at this path. Never point it at source documents or a public web directory. |
| `Storage__MaxFileBytes` | File-size cap (default 20 MiB). Compose exposes `UPLOAD_MAX_BYTES` as its input. The HTTP body also has a bounded allowance for multipart framing. |

Admission checks content, not the user-supplied path: PDF version signature; XLSX ZIP/workbook content type; or UTF-8 CSV with valid quoting and no binary control characters. Single-column text has no distinctive signature and requires explicit `text/csv`. A generic binary MIME type is accepted for identifiable formats. Full document parsing, archive expansion and OCR belong in the bounded reader. New originals use opaque server-generated names. After a crash, check aged unreferenced originals and partial uploads against database metadata before considering cleanup.

## Verify

```sh
dotnet build backend/HackthonSpurs.slnx -c Release
pwsh -NoProfile -File tests/quality/Test-ClassSize.ps1
pwsh -NoProfile -File tests/contract/Test-DocumentContract.ps1
python -B tests/integration/test_document_api.py
dotnet run --project tests/worker/WorkerContract.Tests.csproj -c Release
python -B tests/integration/test_document_worker.py
```

The build requires the .NET 10 SDK; PowerShell 7.6 runs the offline contract checks. The integration suite requires Python 3, Docker and Compose; it builds a private, randomly named project on an ephemeral loopback port, creates its own secret and removes **only that project's** test containers, volumes and image afterwards. It checks real multipart requests, idempotency races, input limits, persisted results/terminal errors, restart durability and a database outage. No external OCR or production credentials are used. If a subnet override is needed for tests, set `COMPOSE_FILE` to both Compose filenames using the platform separator and choose an unused `SPURS_SUBNET`.

For schema work, run `dotnet tool restore` from `backend/`. With the API's native development settings supplied, run `dotnet ef migrations has-pending-model-changes --project HackthonSpurs.Documents --startup-project HackthonSpurs.Api` from `backend/`. Only the API owns migrations. The runtime dependency is the Npgsql EF Core provider; EF design tooling is private to the build/authoring workflow.
