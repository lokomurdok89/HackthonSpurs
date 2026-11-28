import { useCallback, useEffect, useState, type FormEvent } from "react";
import { ApiError } from "../api/errors";
import { getSearchStats, semanticSearch, uploadAndIndexDocument } from "../api/search";
import { ACCEPT_ATTRIBUTE, type SearchStats, type SemanticSearchHit } from "../api/types";
import "../styles/ui.css";
import "./DocumentsSearch.css";

const SAMPLE_QUERIES = [
  "CUSMA country of origin certificate",
  "material grade and heat number",
  "supplier invoice and tariff surcharge",
  "ISO 9001 calibration record",
];

export function DocumentsSearch() {
  const [stats, setStats] = useState<SearchStats | null>(null);
  const [statsError, setStatsError] = useState<string | null>(null);

  const [query, setQuery] = useState("");
  const [topK, setTopK] = useState(8);
  const [searching, setSearching] = useState(false);
  const [searchError, setSearchError] = useState<string | null>(null);
  const [hits, setHits] = useState<SemanticSearchHit[] | null>(null);
  const [activeModel, setActiveModel] = useState<string | null>(null);

  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [uploading, setUploading] = useState(false);
  const [uploadNotice, setUploadNotice] = useState<string | null>(null);
  const [uploadError, setUploadError] = useState<string | null>(null);

  const loadStats = useCallback(async () => {
    setStatsError(null);
    try {
      const next = await getSearchStats();
      setStats(next);
    } catch (err) {
      setStatsError(err instanceof ApiError ? err.problem.title : "Could not reach the backend API.");
    }
  }, []);

  useEffect(() => {
    void loadStats();
  }, [loadStats]);

  async function runSearch(searchText: string) {
    const trimmed = searchText.trim();
    if (!trimmed) return;
    setSearching(true);
    setSearchError(null);
    try {
      const res = await semanticSearch(trimmed, topK);
      setHits(res.results);
      setActiveModel(res.model);
    } catch (err) {
      setHits(null);
      setSearchError(err instanceof ApiError ? err.problem.title : "Search request failed.");
    } finally {
      setSearching(false);
    }
  }

  function onSearchSubmit(e: FormEvent) {
    e.preventDefault();
    void runSearch(query);
  }

  async function onUploadSubmit(e: FormEvent) {
    e.preventDefault();
    if (!selectedFile) return;
    setUploading(true);
    setUploadError(null);
    setUploadNotice(null);
    try {
      const { receipt, indexed } = await uploadAndIndexDocument(selectedFile);
      setUploadNotice(
        `Uploaded ${selectedFile.name} (${receipt.id.slice(0, 8)}…) and indexed ${indexed.chunkCount} chunk(s) with ${indexed.model}.`,
      );
      setSelectedFile(null);
      await loadStats();
    } catch (err) {
      setUploadError(err instanceof ApiError ? err.problem.title : "Upload or indexing failed.");
    } finally {
      setUploading(false);
    }
  }

  return (
    <div className="ds">
      <div className="ds__header">
        <div>
          <h1>Documents &amp; Semantic Search</h1>
          <p className="muted small" style={{ margin: 0 }}>
            Upload PDF, XLSX, or CSV evidence and search across chunks using SPUR embeddings + pgvector.
          </p>
        </div>

        <div className="ds__stats">
          {stats ? (
            <>
              <span className={`chip ${stats.embeddingsConfigured ? "ds__chip--ok" : "ds__chip--warn"}`}>
                {stats.embeddingsConfigured ? `SPUR ready · ${stats.model}` : "SPUR_API_KEY not set"}
              </span>
              <span className="chip ds__chip--neutral">
                {stats.indexedDocuments}/{stats.totalDocuments} docs indexed
              </span>
              <span className="chip ds__chip--neutral">{stats.totalChunks} chunks</span>
            </>
          ) : (
            <span className="chip ds__chip--warn">
              {statsError ? "Backend offline" : "Connecting…"}
            </span>
          )}
          <button type="button" className="btn btn--sm" onClick={() => void loadStats()}>
            Refresh
          </button>
        </div>
      </div>

      {statsError && <div className="ds__alert ds__alert--error">{statsError}</div>}

      <div className="ds__grid">
        <section className="panel">
          <div className="panel__head">
            <h2>Upload &amp; Index Document</h2>
            <span className="muted small">PDF, XLSX, CSV (up to 20 MiB)</span>
          </div>
          <form className="ds__upload-row" onSubmit={(e) => void onUploadSubmit(e)}>
            <input
              type="file"
              accept={ACCEPT_ATTRIBUTE}
              onChange={(e) => {
                setSelectedFile(e.target.files?.[0] ?? null);
                setUploadError(null);
                setUploadNotice(null);
              }}
            />
            <button
              type="submit"
              className="btn btn--primary"
              disabled={!selectedFile || uploading}
            >
              {uploading ? "Uploading & embedding…" : "Upload & index"}
            </button>
          </form>
          {uploadNotice && <div className="ds__alert ds__alert--ok">{uploadNotice}</div>}
          {uploadError && <div className="ds__alert ds__alert--error">{uploadError}</div>}
        </section>

        <section className="panel">
          <div className="panel__head">
            <h2>Semantic Search</h2>
            {activeModel && <span className="muted small mono">model: {activeModel}</span>}
          </div>

          <form className="ds__search-row" onSubmit={onSearchSubmit}>
            <input
              type="search"
              className="ds__input"
              value={query}
              onChange={(e) => setQuery(e.target.value)}
              placeholder="Ask in English or French (e.g. 'Where was steel heat 6061-T6 sourced?')"
              aria-label="Semantic search query"
            />
            <select
              className="ds__select"
              value={topK}
              onChange={(e) => setTopK(Number(e.target.value))}
              aria-label="Result limit"
            >
              <option value={5}>Top 5</option>
              <option value={8}>Top 8</option>
              <option value={15}>Top 15</option>
            </select>
            <button type="submit" className="btn btn--primary" disabled={searching || !query.trim()}>
              {searching ? "Searching…" : "Search"}
            </button>
          </form>

          <div className="ds__samples">
            <span className="muted small">Try:</span>
            {SAMPLE_QUERIES.map((sample) => (
              <button
                key={sample}
                type="button"
                className="ds__sample-btn"
                onClick={() => {
                  setQuery(sample);
                  void runSearch(sample);
                }}
              >
                {sample}
              </button>
            ))}
          </div>

          {searchError && <div className="ds__alert ds__alert--error">{searchError}</div>}

          {hits !== null && (
            <div className="ds__results">
              {hits.length === 0 ? (
                <p className="muted small" style={{ margin: 0 }}>
                  No indexed chunks matched yet. Upload and index a document above to populate the vector store.
                </p>
              ) : (
                hits.map((hit) => (
                  <article key={hit.chunkId} className="ds__hit">
                    <div className="ds__hit-head">
                      <div className="ds__hit-meta">
                        <span className="strong">{hit.sourceName ?? `Document ${hit.documentId.slice(0, 8)}`}</span>
                        <span className="chip ds__chip--neutral">{hit.locator}</span>
                        <span className="muted mono small">{hit.documentId.slice(0, 8)}</span>
                      </div>
                      <span className="chip ds__chip--ok">
                        {Math.round(hit.score * 100)}% match
                      </span>
                    </div>
                    <p className="ds__hit-text">{hit.text}</p>
                  </article>
                ))
              )}
            </div>
          )}
        </section>
      </div>
    </div>
  );
}
