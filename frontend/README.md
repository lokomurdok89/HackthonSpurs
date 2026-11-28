# Origin Vault · Local frontend

Use Node 22.12+ or 24 and the committed npm lockfile. Start the API and PostgreSQL using the [root runbook](../README.md#run-locally). From the repository root:

```sh
npm --prefix frontend ci
npm --prefix frontend run dev:api
```

Open the printed loopback URL and select **Documents**. In **Local API** mode, the Vite development server forwards `/api/` requests to the loopback API (`API_PORT` from the root Compose environment, default 5080). Do not enable LAN access. No CORS change, credentials in browser code or public endpoint is needed.

1. Select a synthetic PDF, XLSX or CSV and upload it. A receipt link identifies this operation. A retry after a lost response reuses the same file and key while the form remains open.
2. Bookmark the receipt link. Navigate away or reload it to resume polling **without uploading again**. Keep a link for each file. No file bytes are stored in browser storage.
3. With default Compose, uploads stay **Queued · Attempt 0**. To see **Completed** and its located fragment without OCR, enable the [synthetic Worker profile](../README.md#run-the-synthetic-worker-demo) and upload the exact demo CSV. The view handles terminal failure, or displays returned fragments and warnings with their original locators; it does not decide origin or CUSMA eligibility.

**Without a backend:** use `npm --prefix frontend run dev` for the labelled in-memory mock; reloading clears mock receipts. Products and Review queue are demo screens, independent of real uploads. They default to the committed synthetic sample. Only for an authorized private demo, explicitly opt into a local override with `VITE_LOCAL_DEMO=true` in default development mode; private demo modules are not part of production builds. Never put secrets in `VITE_*` variables.

## Checks

```sh
npm --prefix frontend run lint
npm --prefix frontend run build
python -B tests/integration/test_document_ui.py
```

The browser smoke test requires local Docker, Python Playwright, Chromium and the web dependencies above. Set `SPURS_BROWSER_CHANNEL=chrome` to use an installed Google Chrome. It starts an isolated PostgreSQL/API stack and temporary Vite server, uses synthetic input, simulates a lost receipt, verifies replay and reload, seeds a separate warning example privately, and independently uploads the exact demo CSV and displays the real Worker publication. It never runs OCR or imports challenge data.
