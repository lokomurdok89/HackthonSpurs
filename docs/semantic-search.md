# Semantic search for SPUR readings

Choose **Send to OCR** once on the **Documents** form. Uploading returns a durable receipt, not a completed result. The OCR worker reads printed pages and stores a validated transcript. In that same database operation, it creates one indexing job for the receipt. The index worker uses only the stored text and its one-based page locators, never the original file. It does not send a page image or an original filename to the embedding provider.

Read and index states are independent. An index failure leaves the transcript, receipt and warnings available. Blank readings show **Skipped** without an embedding request. Local-only uploads, the synthetic fixture and failed OCR jobs are not eligible. Readings completed before this integration and legacy PR #7 chunks are not backfilled.

## Enable the optional stack

The default and synthetic stacks do not contact SPUR. With a private key and an approved policy for the input data, follow the [OCR overlay](spur-ocr.md). Start the index worker alongside the OCR services:

```sh
docker compose -f compose.yaml -f compose.spur.yaml up --build -d api spur-reader spur-worker spur-index-worker
```

The API and embedding worker share the following optional Compose settings. Never put them in a browser environment, committed files, logs, screenshots or support requests.

| Setting | Meaning |
|---|---|
| `SPUR_EMBEDDINGS_ENABLED` | Defaults to `true` in the optional overlay. To run OCR only, set `false` and leave `spur-index-worker` stopped. Future readable OCR results still create durable jobs. |
| `SPUR_API_KEY` | Private server-side key; no default. The API uses it only for explicitly submitted search queries. The index worker uses it only for durable OCR transcripts. |
| `SPUR_BASE_URL` | Defaults to `https://ai.spuric.com/v1`. Production permits only this HTTPS origin/path and disables redirects, proxying and cookies. |
| `SPUR_EMBED_MODEL` | Defaults to `spur-embed`, the embeddings ID in the [public SPUR catalog](https://ai.spuric.com/api/catalog). |
| `SPUR_EMBED_DIMENSIONS` | Defaults to `768` for the catalog's Nomic Embed model. The API and worker reject vectors of the wrong size. |
| `SPUR_EMBED_VERSION` | Defaults to `1`, a local vector-space revision. Change it in **both** hosts if the upstream encoder changes beneath the same model ID. |

Search ranks only completed chunks compatible with the current model, revision and dimensions. Old vectors remain stored but are not queried; no automatic re-embedding occurs after a profile change. The recorded index status describes the completed attempt, not whether its profile still matches the active search.

## Search and evidence

From an indexed receipt, select **Search this document**, or use **Search** in the sidebar to search all current indexes. Submitting a nonempty query can send that query text to SPUR. No external request is needed to render a receipt, view index status, load search counts or search a scope with no compatible chunks. Instead of searching for sensitive details, use a synthetic case unless transfer has been explicitly authorized.

Each result shows a technical text excerpt and a similarity score, not confidence, proof of origin or an eligibility verdict. Open a hit to focus the matching page transcript on the original receipt. The API does not expose the actual file or a public URL for it.

## Failure and retry boundaries

- The index worker uses a separate lease and database attempt counter. It retries transient provider failures after 5 s and 30 s, with at most three claims. Each provider operation has an eight-second request/body budget; an attempt has up to four minutes and a six-minute lease. A lost lease cannot publish, and vector replacement and terminal index state commit together.
- A repeated upload with the same key, bytes and mode reuses the receipt and does not create an index attempt. Retries or lease recovery after an ambiguous external response can repeat an embedding request, so billing is not exactly-once. OCR is not resent by the index worker.
- An incompatible or malformed provider response fails safely without writing partial chunks. Logs and public errors do not contain provider bodies, original names, query text or transcripts.

Run `python -B tests/integration/test_semantic_index.py` to exercise the API, database, worker leases and browser against private deterministic stubs. It uses a randomly named Compose project, a project-specific database image tag and synthetic credentials. Docker Compose uses an explicit test environment file instead of the developer's root `.env`. The browser uses an explicit local test API port. The suite does not contact SPUR. This verifies the contract in isolation, not external account access, billing, provider accuracy or data-transfer approval. See the [API handoff](api/README.md) for the wire routes.

For the observed local acceptance results, migration rehearsal and outstanding release gate, see the [integration review](ocr-index-validation.md).
