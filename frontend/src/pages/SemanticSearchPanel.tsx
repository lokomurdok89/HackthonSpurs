import { useState, type FormEvent } from "react";
import { ApiError } from "../api/errors";
import { semanticSearch } from "../api/search";
import type { SemanticSearchHit } from "../api/types";
import { FileIcon, SearchIcon } from "../components/icons";
import { SearchExcerpt } from "./SearchExcerpt";

const SAMPLE_QUERIES = [
  "CUSMA country of origin certificate",
  "material grade and heat number",
  "supplier invoice and tariff surcharge",
  "ISO 9001 calibration record",
];

export function SemanticSearchPanel({ onSelectDocument }: { onSelectDocument: (id: string) => void }) {
  const [query, setQuery] = useState("");
  const [topK, setTopK] = useState(8);
  const [searching, setSearching] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [hits, setHits] = useState<SemanticSearchHit[] | null>(null);
  const [lastQuery, setLastQuery] = useState("");
  const [activeModel, setActiveModel] = useState<string | null>(null);

  async function runSearch(searchText: string) {
    const trimmed = searchText.trim();
    if (!trimmed) return;
    setSearching(true);
    setError(null);
    try {
      const res = await semanticSearch(trimmed, topK);
      setHits(res.results);
      setLastQuery(res.query);
      setActiveModel(res.model);
    } catch (err) {
      setHits(null);
      setError(err instanceof ApiError ? err.problem.title : "Semantic search request failed.");
    } finally {
      setSearching(false);
    }
  }

  function onSubmit(e: FormEvent) {
    e.preventDefault();
    void runSearch(query);
  }

  return (
    <section className="panel" aria-label="Semantic search">
      <div className="panel__head">
        <h2>Semantic Search</h2>
        {activeModel && <span className="muted small mono">{activeModel}</span>}
      </div>

      <form className="docs__search-form" onSubmit={onSubmit}>
        <div className="docs__search-wrap">
          <SearchIcon size={16} className="docs__search-icon" />
          <input
            type="search"
            className="docs__search-input"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder="Search evidence in English or French (e.g. 'steel heat 6061-T6 origin')…"
            aria-label="Semantic search query"
          />
        </div>
        <select
          className="docs__select"
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

      <div className="docs__samples">
        <span className="muted small">Try:</span>
        {SAMPLE_QUERIES.map((sample) => (
          <button
            key={sample}
            type="button"
            className="docs__pill"
            onClick={() => {
              setQuery(sample);
              void runSearch(sample);
            }}
          >
            {sample}
          </button>
        ))}
      </div>

      {error && <div className="docs__alert docs__alert--error" role="alert">{error}</div>}

      <div className="docs__results">
        {hits === null ? (
          <p className="docs__empty">
            Search across all indexed PDFs, spreadsheets, CSVs, and scanned photos by meaning.
          </p>
        ) : hits.length === 0 ? (
          <p className="docs__empty">
            No indexed chunks matched yet. Upload and index a document on the right to populate the vector store.
          </p>
        ) : (
          <>
            <div className="docs__results-meta">
              <span className="muted small">
                {hits.length} result{hits.length === 1 ? "" : "s"} for{" "}
                <strong className="strong">“{lastQuery}”</strong>
              </span>
            </div>
            {hits.map((hit, idx) => {
              const pct = Math.round(hit.score * 100);
              const tier = pct >= 70 ? "high" : pct >= 50 ? "mid" : "low";
              return (
                <article key={hit.chunkId} className="docs__hit">
                  <div className="docs__hit-head">
                    <div className="docs__hit-meta">
                      <span className="docs__rank">#{idx + 1}</span>
                      <FileIcon size={15} className="docs__hit-icon" />
                      <span className="docs__hit-title">
                        {hit.sourceName ?? `Document ${hit.documentId.slice(0, 8)}`}
                      </span>
                      <span className="chip docs__chip--loc">{hit.locator}</span>
                    </div>
                    <div className="docs__score">
                      <span className="docs__score-bar" aria-hidden="true">
                        <span
                          className={`docs__score-fill docs__score-fill--${tier}`}
                          style={{ width: `${Math.max(8, pct)}%` }}
                        />
                      </span>
                      <span className={`chip docs__chip--score docs__chip--${tier}`}>
                        {pct}% match
                      </span>
                    </div>
                  </div>
                  <SearchExcerpt text={hit.text} query={lastQuery} />
                  <div className="docs__hit-foot">
                    <span className="mono">{hit.documentId.slice(0, 8)}… · chunk #{hit.chunkIndex + 1}</span>
                    <button
                      type="button"
                      className="docs__link-btn"
                      onClick={() => onSelectDocument(hit.documentId)}
                    >
                      View document →
                    </button>
                  </div>
                </article>
              );
            })}
          </>
        )}
      </div>
    </section>
  );
}
