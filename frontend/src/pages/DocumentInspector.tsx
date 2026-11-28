import { useCallback, useEffect, useState } from "react";
import { ApiError } from "../api/errors";
import { getDocumentDetail, reindexDocument } from "../api/search";
import type { DocumentDetailView } from "../api/types";
import { SearchExcerpt } from "./SearchExcerpt";

function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

export function DocumentInspector({
  id,
  onClose,
  onReindexed,
}: {
  id: string;
  onClose: () => void;
  onReindexed: () => void;
}) {
  const [detail, setDetail] = useState<DocumentDetailView | null>(null);
  const [loading, setLoading] = useState(true);
  const [reindexing, setReindexing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setDetail(await getDocumentDetail(id));
    } catch (err) {
      setDetail(null);
      setError(err instanceof ApiError ? err.problem.title : "Could not load document chunks.");
    } finally {
      setLoading(false);
    }
  }, [id]);

  useEffect(() => {
    setNotice(null);
    void load();
  }, [load]);

  async function handleReindex() {
    setReindexing(true);
    setError(null);
    setNotice(null);
    try {
      const res = await reindexDocument(id, detail?.sourceName);
      setNotice(`Re-indexed ${res.chunkCount} chunk(s) with ${res.model}.`);
      await load();
      onReindexed();
    } catch (err) {
      setError(err instanceof ApiError ? err.problem.title : "Re-indexing failed.");
    } finally {
      setReindexing(false);
    }
  }

  return (
    <section className="panel docs__inspector" aria-label="Document inspector">
      <div className="panel__head">
        <div>
          <h2>{detail?.sourceName ?? `Document ${id.slice(0, 8)}…`}</h2>
          <p className="muted small mono" style={{ margin: "2px 0 0" }}>
            {id}
            {detail ? ` · ${detail.mediaType} · ${formatBytes(detail.byteCount)} · ${detail.chunks.length} chunk(s)` : ""}
          </p>
        </div>
        <div className="docs__inspector-actions">
          <button type="button" className="btn btn--sm" disabled={reindexing || loading} onClick={() => void handleReindex()}>
            {reindexing ? "Re-indexing…" : "Re-index"}
          </button>
          <button type="button" className="btn btn--sm btn--ghost" onClick={onClose}>
            Close
          </button>
        </div>
      </div>

      {notice && <div className="docs__alert docs__alert--ok">{notice}</div>}
      {error && <div className="docs__alert docs__alert--error" role="alert">{error}</div>}

      {loading ? (
        <p className="muted small" style={{ margin: "14px 0 0" }}>Loading extracted chunks…</p>
      ) : detail && detail.chunks.length === 0 ? (
        <p className="docs__empty" style={{ marginTop: 14 }}>
          No chunks indexed for this document yet. Click <strong>Re-index</strong> to extract text and generate SPUR embeddings.
        </p>
      ) : detail ? (
        <div className="docs__chunks">
          {detail.chunks.map((chunk) => (
            <article key={chunk.chunkId} className="docs__chunk">
              <div className="docs__hit-head">
                <span className="chip docs__chip--loc">{chunk.locator}</span>
                <span className="muted small mono">chunk #{chunk.chunkIndex + 1} · {chunk.model}</span>
              </div>
              <SearchExcerpt text={chunk.text} />
            </article>
          ))}
        </div>
      ) : null}
    </section>
  );
}
