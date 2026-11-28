import { useEffect, useRef, useState, type FormEvent } from "react";
import { apiMode } from "../api/client";
import { ApiError } from "../api/errors";
import { getSearchStats, semanticSearch } from "../api/search";
import type { SearchStats, SemanticSearchHit, SemanticSearchResponse } from "../api/types";
import { SearchExcerpt } from "./SearchExcerpt";
import "./DocumentsSearch.css";
import "./Documents.css";

const RECEIPT_PATTERN = /^[\da-f]{8}(?:-[\da-f]{4}){3}-[\da-f]{12}$/i;

/** Map API search errors to fixed messages that do not display provider diagnostics.
 * @param error - Request failure to classify.
 */
function searchError(error: unknown) {
  if (error instanceof ApiError && error.problem.code === "embeddings_not_configured") return "Semantic search is not enabled.";
  if (error instanceof ApiError && error.problem.code === "invalid_search") return "Enter a valid search query.";
  return "Search is temporarily unavailable. Try again in a moment.";
}

/** Render one search hit and link back to its exact OCR page in the source receipt.
 * @param props - Component inputs.
 * @param props.query - Submitted query used for highlighting.
 * @param props.hit - Ranked excerpt with a page locator.
 */
function SearchHit({ hit, query }: { hit: SemanticSearchHit; query: string }) {
  const { page } = hit.pageLocator;
  return (
    <li className="ds__hit">
      <div className="ds__hit-head">
        <a href={`#/documents/${hit.documentId}/page/${page}`}>Open reading · Page {page}</a>
        <span>Similarity {Math.round(hit.score * 100)}%</span>
      </div>
      <small>Document {hit.documentId.slice(0, 8)} · {hit.model}</small>
      <SearchExcerpt text={hit.text} query={query} />
    </li>
  );
}

/** Show a read-only search workspace; uploads stay on the Documents form.
 * @param props - Component inputs.
 * @param props.embedded - Show the panel inside the document workspace.
 * @param props.documentId - Optional receipt scope supplied by the route.
 */
export function DocumentsSearch({ documentId, embedded = false }: { documentId: string; embedded?: boolean }) {
  const [stats, setStats] = useState<SearchStats>();
  const [statsError, setStatsError] = useState(false);
  const [revision, setRevision] = useState(0);
  const [topK, setTopK] = useState(8);
  const [query, setQuery] = useState("");
  const [response, setResponse] = useState<SemanticSearchResponse>();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const active = useRef<AbortController | null>(null);

  useEffect(() => {
    if (apiMode !== "http") return;
    const controller = new AbortController();
    const { signal } = controller;
    void getSearchStats(signal).then((value) => {
      if (!signal.aborted) { setStats(value); setStatsError(false); }
    }).catch(() => {
      if (!signal.aborted) { setStats(undefined); setStatsError(true); }
    });
    return () => controller.abort();
  }, [revision]);

  useEffect(() => () => active.current?.abort(), []);

  const validScope = !documentId || RECEIPT_PATTERN.test(documentId);
  /** Submit the bounded query and render results only while this search view is active.
   * @param event - Semantic search form submission.
   */
  async function submit(event: FormEvent) {
    event.preventDefault();
    if (!validScope || !query.trim() || active.current || !stats?.embeddingsConfigured) return;
    const controller = new AbortController();
    active.current = controller;
    setBusy(true);
    setError("");
    setResponse(undefined);
    try {
      const value = await semanticSearch(query.trim(), documentId, topK, controller.signal);
      if (!controller.signal.aborted) setResponse(value);
    } catch (reason) {
      if (!controller.signal.aborted) setError(searchError(reason));
    } finally {
      if (!controller.signal.aborted) setBusy(false);
      active.current = null;
    }
  }

  return (
    <section className="ds">
      <header className="ds__header">
        <div>
          {embedded ? <h2>Semantic search</h2> : <h1>Semantic search</h1>}
          <p>Search indexed OCR text. Each excerpt links to its page in the reading.</p>
        </div>
        {!embedded && <a href="#/documents">Go to Documents</a>}
      </header>
      {apiMode !== "http" ? <p className="notice">Search requires the local API and SPUR indexing; no mock results are shown.</p> :
        <div className="panel">
          {stats && <p>{stats.embeddingsConfigured
            ? `${stats.indexedDocuments}/${stats.totalDocuments} indexed documents · ${stats.totalChunks} excerpts · ${stats.model}`
            : "Semantic search is not enabled."}</p>}
          {!stats && !statsError && <p role="status">Checking search availability…</p>}
          {statsError && <p role="alert">Cannot check search availability. Refresh to try again.</p>}
          <button className="btn btn--sm" type="button" onClick={() => setRevision((n) => n + 1)}>Refresh index stats</button>
          {documentId && validScope && <p>Searching one receipt. <a href="#/search">Search all documents</a></p>}
          {!validScope && <p role="alert">Invalid document link. <a href="#/search">Search all documents</a></p>}
          <form onSubmit={submit}>
            <label htmlFor="semantic-query">Search query</label>
            <div className="ds__search-row">
              <input className="ds__input" type="search" id="semantic-query" value={query} maxLength={1000} required
                disabled={busy || !stats?.embeddingsConfigured || !validScope}
                onChange={(event) => setQuery(event.target.value)} />
              <select className="ds__select" aria-label="Result limit" value={topK} disabled={busy}
                onChange={(e) => setTopK(Number(e.target.value))}>
                {[5, 8, 15].map((value) => <option key={value} value={value}>Top {value}</option>)}
              </select>
              <button type="submit" className="btn btn--primary"
                disabled={busy || !query.trim() || !stats?.embeddingsConfigured || !validScope}>
                {busy ? "Searching…" : "Search"}
              </button>
            </div>
          </form>
          <div className="ds__samples" aria-label="Example queries">
            {["CUSMA country of origin certificate", "material grade and heat number", "supplier invoice and tariff surcharge", "ISO 9001 calibration record"].map((sample) =>
              <button className="btn btn--sm" type="button" disabled={busy} key={sample} onClick={() => setQuery(sample)}>{sample}</button>)}
          </div>
          <p className="ds__hint">Submitting sends the query text to SPUR. Similarity indicates related text, not verified evidence.</p>
          {error && <p role="alert">{error}</p>}
          {response && <div aria-live="polite">
            <h2>Search results</h2>
            {response.results.length === 0
              ? <p>No searchable excerpts for this scope.</p>
              : <ol className="ds__results">{response.results.map((hit) => <SearchHit key={hit.chunkId} hit={hit} query={response.query} />)}</ol>}
          </div>}
        </div>}
    </section>
  );
}
