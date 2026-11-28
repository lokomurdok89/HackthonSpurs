# HackthonSpurs · Document API

Local, authenticated shared-workspace API for the Track 2 demo. It accepts PDF, XLSX and CSV uploads, stores the original privately, and queues one durable PostgreSQL job per operation. It serves status and persisted reading results using the [published OpenAPI contract](docs/api/openapi.json). **Default: API and database only.** The optional [synthetic Worker demo](#run-the-synthetic-worker-demo) processes one known CSV. It is not OCR. Other PDF/XLSX/CSV uploads stay queued. For document OCR, enable the SPUR overlay below.

- [Optional isolated SPUR vision reader](docs/spur-ocr.md) — one authorized page image per request; document upload, queued reading and transcript in Documents.
- [Optional semantic search](docs/semantic-search.md) — automatically index readable OCR transcripts, then follow ranked excerpts to their original page.
- [Workspace roles, first administrator and evaluation walkthrough](docs/workspace-access.md)
- [Diagnose an existing receipt with a workspace login, without resubmitting](docs/receipt-diagnostics.md)
- [Architecture and first demo walkthrough](docs/ARCHITECTURE.md)
- [Run the React UI against the local API](frontend/README.md)
- [Frontend integration guide and synthetic examples](docs/api/README.md)
- [Live API tests against PostgreSQL](tests/integration/test_document_api.py)
- [DeValpo engineering skill](.agents/skills/devalpo-engineering/SKILL.md) — from this repository, ask Codex: `Use $devalpo-engineering to review this change and explain which checks it needs.`

## Try the local demo without an .env

From a fresh clone of `main`, run this **standalone** Compose file (Linux containers,
Docker Compose 2.24.4+). No passwords, SPUR account or other environment settings are required:

```sh
docker compose -f compose.demo.yaml up --build -d --wait
```

Open **http://127.0.0.1:8081**. Every fresh demo uses the same public fixture accounts:

| Role | Username | Local demo password |
| --- | --- | --- |
| Administrator | `demo.admin` | `LocalDemo-Admin-2026` |
| Reader | `demo.lector` | `LocalDemo-Reader-2026` |

These are public test values, **not secrets**. Use invented data only; never publish this port,
forward it through a tunnel or proxy, or combine this file with the full SPUR stack.
The demo has its own database and originals volumes. Its offline worker processes the
[synthetic CSV](backend/Testing/HackthonSpurs.Reader.Fixture/worker-demo.csv); it does **not**
call live OCR or embeddings. For real provider integration, use the private full stack below.
Restarting does not overwrite a password changed under **Users**. To stop the demo and
keep its data, run `docker compose -f compose.demo.yaml down`.
If Docker reports exhausted address pools, choose an **unused** private `SPURS_SUBNET` for
this demo (not the live stack's subnet) and append `-f compose.subnet.yaml` after the
demo file in each command.

## Run the complete application on one Docker host

Use Linux containers and a current Docker Compose with `include` and `!reset` support.
No host Node.js or .NET install is needed. This starts the compiled React UI, API,
PostgreSQL/pgvector, private SPUR reader, OCR worker and semantic index worker.

1. From the repository root, create a **private** `.env` from [.env.example](.env.example)
   **only if it does not already exist**. Set a unique `POSTGRES_PASSWORD`, a real
   `SPUR_API_KEY`, and the authorized OCR and embedding settings from the example.
   Keep embeddings enabled for the complete stack. On an empty database, configure
   [first-admin bootstrap or local demo accounts](docs/workspace-access.md).
2. Run `docker compose -f compose.full.yaml up --build -d --wait`. Open
   **http://127.0.0.1:8080** (change `WEB_PORT` if needed). Sign in as an administrator,
   select **Documents**, and send an authorized synthetic invoice to OCR. Follow the
   receipt to its transcript and index status. A healthy gateway is not a live SPUR test.
3. Check services with `docker compose -f compose.full.yaml ps`. To stop, run
   `docker compose -f compose.full.yaml down` **without `-v`**. Use the same
   project name on subsequent starts. This entry point defaults to a separate
   `hackthonspurs-full` project: it will not import another demo's data.
   Keep the named database and originals volumes on persistent storage and back up both.
4. For unattended recovery, enable Docker at host boot. The six long-running services
   use `restart: unless-stopped`; manually stopped or removed containers are not resurrected.
   If the host has exhausted its address pools, select an unused `SPURS_SUBNET` and
   add `-f compose.subnet.yaml` after the full file in each command.

Only the gateway is published, on loopback. On a remote VM, use an SSH tunnel
(`ssh -L 8080:127.0.0.1:8080 user@host`) for the private demo. For a shared HTTPS URL,
terminate TLS at a trusted host proxy, route the whole origin to the loopback gateway,
and **overwrite** `X-Forwarded-Proto` with `https` so the session cookie is marked Secure.
Do not publish the API, reader or database. The login rate limit is shared at the gateway;
this is a single shared workspace, not a public multi-tenant deployment.

Offline smoke test: `python -B tests/integration/test_full_stack.py` (Python Playwright and
Chromium required; optionally set `SPURS_BROWSER_CHANNEL` and unused `SPURS_TEST_SUBNET`).
It uses disposable volumes, synthetic input and private stand-ins for OCR and embeddings.

## Run locally

Use Docker with Linux containers and Docker Compose v2. From the repository root:

1. Create a local `.env` from [.env.example](.env.example) **only if one does not exist**. Set `POSTGRES_PASSWORD` to a random, unique local secret and configure the [first administrator](docs/workspace-access.md#set-up-the-first-administrator). Keep it private and reuse it for the lifetime of the database volume. Do not publish the resolved Compose configuration.
2. Run `docker compose up --build -d` and check `docker compose ps`. PostgreSQL has no host port. The API runs on loopback at `http://127.0.0.1:5080` by default and applies versioned EF migrations before listening. Use a same-origin dev proxy for a separate UI dev server; keep the local demo off the LAN.
3. Stop with `docker compose down`. The database and original-file volumes survive. No challenge files are mounted. On hosts whose Docker address pools are exhausted, set `SPURS_SUBNET` to an **unused private CIDR** and use `docker compose -f compose.yaml -f compose.subnet.yaml up --build -d`. No other project's networks need to change.

### Run the synthetic Worker demo

After configuring the local Compose secret above, run:

```sh
docker compose --profile synthetic up --build -d
npm --prefix frontend ci
npm --prefix frontend run dev:api
```

In **Documents**, check the **Local API** label and upload the [exact demo CSV](backend/Testing/HackthonSpurs.Reader.Fixture/worker-demo.csv) from disk. Do not recreate or resave it: its bytes are allowlisted by SHA-256. Follow the receipt until **Completed · Attempt 1** (the short Processing state may be missed). Expect **synthetic-fixture/1**, **Row 2, column 1**, and **DEMO-ONLY-014**. Reload the receipt to read the same persisted result. A modified copy or any other file stays queued. No result seeding, document comparison or OCR is involved.

Stop this profile with `docker compose --profile synthetic down` (no `-v`); the demo job and result survive. The profile also picks up previously queued copies of the exact fixture. No provider endpoint, key or challenge data is needed. See the [internal contract and recovery notes](docs/reader-fixture.md).

### Try an upload and check its status

From PowerShell 7 at the Git root, sign in as an administrator and submit the synthetic fixture. The session stays in memory:

```powershell
$api = 'http://127.0.0.1:5080'  # Adjust if API_PORT was changed.
$credential = Get-Credential -Message 'Local Origin Vault administrator'
$headers = @{ 'X-Workspace-Request' = '1' }
$null = Invoke-RestMethod "$api/api/auth/login" -Method Post -ContentType application/json -Headers $headers -SessionVariable workspace -Body (@{ username = $credential.UserName; password = $credential.GetNetworkCredential().Password } | ConvertTo-Json -Compress)
$headers['Idempotency-Key'] = [guid]::NewGuid().ToString()
$receipt = Invoke-RestMethod "$api/api/documents" -Method Post -Headers $headers -WebSession $workspace -Form @{ file = Get-Item './backend/Testing/HackthonSpurs.Reader.Fixture/worker-demo.csv' }
Invoke-RestMethod "$api/api/documents/$($receipt.id)" -WebSession $workspace
Invoke-RestMethod "$api/api/auth/logout" -Method Post -Headers $headers -WebSession $workspace
```

Follow the relative `Location` with GET. The status starts at `Queued`, attempt 0; appending `/result` returns `409/result_not_ready`. Replay the same file and key to recover the receipt after an interrupted response. Use a new key only for a new operation. A result becomes readable only after atomic publication by a Worker. There is no public result-seeding or original-download endpoint.

## Configuration and storage

| Setting | Owner and meaning |
|---|---|
| `POSTGRES_PASSWORD` | Required Compose secret. Configures the local database and supplies the API password through `PGPASSWORD`; not part of any HTTP payload. |
| `Auth__DemoSeedEnabled`, `Auth__DemoAdminPassword`, `Auth__DemoReaderPassword` | Optional [two-account demo setup](docs/workspace-access.md#seed-the-local-demo). Initial credentials are used only for missing accounts. |
| `Auth__BootstrapUsername`, `Auth__BootstrapPassword` | Server-only first administrator provisioning; see [workspace access](docs/workspace-access.md). No default credential or open registration. |
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
python -B tests/integration/test_workspace_auth.py
python -B tests/integration/test_workspace_session_lookup.py
python -B tests/integration/test_workspace_ui.py
# Pure use cases: in-memory ports only; no database, provider or Docker.
dotnet run --project tests/application/Application.Tests.csproj -c Release
dotnet run --project tests/worker/WorkerContract.Tests.csproj -c Release
python -B tests/integration/test_document_worker.py
python -B tests/integration/test_semantic_index.py
```

The build requires the .NET 10 SDK; PowerShell 7.6 runs the offline contract checks. The semantic browser suite additionally requires the [frontend and Playwright setup](frontend/README.md#checks). The integration suite requires Python 3, Docker and Compose; it builds a private, randomly named project on an ephemeral loopback port, creates its own secret and removes **only that project's** test containers, volumes and image afterwards. It checks real multipart requests, idempotency races, input limits, persisted results/terminal errors, restart durability and a database outage. No external OCR or production credentials are used. If a subnet override is needed for tests, set `COMPOSE_FILE` to both Compose filenames using the platform separator and choose an unused `SPURS_SUBNET`.

For schema work, use the EF CLI on the same major version as the backend (.NET 10). With the API's native development settings supplied, run `dotnet ef migrations has-pending-model-changes --project Adapters/HackthonSpurs.Persistence --startup-project Hosts/HackthonSpurs.Api` from `backend/`. Only the API owns migrations. The runtime dependency is the Npgsql EF Core provider; EF design tooling is private to the build/authoring workflow.
