import { useCallback, useEffect, useRef, useState, type DragEvent, type FormEvent } from "react";
import { ApiError } from "../api/errors";
import { getSearchStats, uploadAndIndexDocument } from "../api/search";
import { ACCEPT_ATTRIBUTE, type SearchStats } from "../api/types";
import { FileIcon } from "../components/icons";
import { DocumentInspector } from "./DocumentInspector";
import { SemanticSearchPanel } from "./SemanticSearchPanel";
import "../styles/ui.css";
import "./Documents.css";

function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

export function Documents({ documentId }: { documentId: string }) {
  const [stats, setStats] = useState<SearchStats | null>(null);
  const [statsError, setStatsError] = useState<string | null>(null);
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [dragActive, setDragActive] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [uploadNotice, setUploadNotice] = useState<string | null>(null);
  const [uploadError, setUploadError] = useState<string | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const inspectorRef = useRef<HTMLDivElement>(null);

  function openDocument(id: string) {
    const targetHash = "#/documents/" + encodeURIComponent(id);
    if (window.location.hash !== targetHash) {
      window.location.hash = "/documents/" + encodeURIComponent(id);
    }
    requestAnimationFrame(() => {
      inspectorRef.current?.scrollIntoView({ behavior: "smooth", block: "start" });
    });
  }

  const loadStats = useCallback(async () => {
    setStatsError(null);
    try {
      setStats(await getSearchStats());
    } catch (err) {
      setStatsError(err instanceof ApiError ? err.problem.title : "Could not reach the backend API.");
    }
  }, []);

  useEffect(() => { void loadStats(); }, [loadStats]);

  function pickFile(file: File | null) {
    setSelectedFile(file);
    setUploadError(null);
    setUploadNotice(null);
  }

  function onDrop(e: DragEvent<HTMLLabelElement>) {
    e.preventDefault();
    setDragActive(false);
    const file = e.dataTransfer.files?.[0] ?? null;
    if (file) pickFile(file);
  }

  async function onUploadSubmit(e: FormEvent) {
    e.preventDefault();
    if (!selectedFile || uploading) return;
    setUploading(true);
    setUploadError(null);
    setUploadNotice(null);
    try {
      const { receipt, indexed } = await uploadAndIndexDocument(selectedFile);
      setUploadNotice(
        `Indexed ${selectedFile.name} (${indexed.chunkCount} chunk${indexed.chunkCount === 1 ? "" : "s"} via ${indexed.model}).`,
      );
      setSelectedFile(null);
      if (fileInputRef.current) fileInputRef.current.value = "";
      window.location.hash = "/documents/" + encodeURIComponent(receipt.id);
      await loadStats();
    } catch (err) {
      setUploadError(err instanceof ApiError ? err.problem.title : "Upload or indexing failed.");
    } finally {
      setUploading(false);
    }
  }

  const validDocId = /^[\da-f]{8}(?:-[\da-f]{4}){3}-[\da-f]{12}$/i.test(documentId);

  return (
    <div className="docs">
      <header className="docs__hero">
        <div>
          <h1>Documents</h1>
          <p className="docs__subtitle">
            Upload evidence files, inspect receipts, and search indexed chunks with SPUR embeddings &amp; Vision OCR.
          </p>
        </div>
        <button type="button" className="btn btn--sm" onClick={() => void loadStats()}>
          Refresh status
        </button>
      </header>

      <section className="docs__metrics" aria-label="Indexing status">
        <div className="docs__metric">
          <span className="docs__metric-label">Embeddings</span>
          <span className={`stamp ${stats?.embeddingsConfigured ? "docs__stamp--ok" : "docs__stamp--warn"}`}>
            {stats ? (stats.embeddingsConfigured ? stats.model : "SPUR_API_KEY unset") : statsError ? "Offline" : "Connecting…"}
          </span>
        </div>
        <div className="docs__metric">
          <span className="docs__metric-label">Vision OCR</span>
          <span className={`stamp ${stats?.visionOcrConfigured ? "docs__stamp--ok" : "docs__stamp--muted"}`}>
            {stats ? (stats.visionOcrConfigured ? (stats.visionModel ?? "Enabled") : "Off") : "—"}
          </span>
        </div>
        <div className="docs__metric">
          <span className="docs__metric-label">Indexed docs</span>
          <span className="docs__metric-value">
            {stats ? `${stats.indexedDocuments} / ${stats.totalDocuments}` : "—"}
          </span>
        </div>
        <div className="docs__metric">
          <span className="docs__metric-label">Vector chunks</span>
          <span className="docs__metric-value">{stats ? stats.totalChunks.toLocaleString() : "—"}</span>
        </div>
      </section>

      {statsError && <div className="docs__alert docs__alert--error" role="alert">{statsError}</div>}

      <div className="docs__workspace">
        <SemanticSearchPanel onSelectDocument={openDocument} />

        <section className="panel" aria-label="Upload and index document">
          <div className="panel__head">
            <h2>Upload &amp; Index</h2>
            <span className="muted small">Up to 20 MiB</span>
          </div>

          <form onSubmit={(e) => void onUploadSubmit(e)}>
            <label
              htmlFor="document-file"
              className={`docs__dropzone ${dragActive ? "docs__dropzone--active" : ""}`}
              onDragOver={(e) => { e.preventDefault(); setDragActive(true); }}
              onDragLeave={() => setDragActive(false)}
              onDrop={onDrop}
            >
              <FileIcon size={24} className="docs__dropzone-icon" />
              <span className="docs__dropzone-title">Drop a file here or click to browse</span>
              <span className="docs__dropzone-hint">PDF, XLSX, CSV, PNG, or JPEG</span>
              <input
                ref={fileInputRef}
                id="document-file"
                type="file"
                className="visually-hidden"
                accept={ACCEPT_ATTRIBUTE}
                disabled={uploading}
                onChange={(e) => pickFile(e.target.files?.[0] ?? null)}
              />
            </label>

            {selectedFile && (
              <div className="docs__selected">
                <span className="docs__selected-name" title={selectedFile.name}>{selectedFile.name}</span>
                <span className="muted small mono">{formatBytes(selectedFile.size)}</span>
              </div>
            )}

            <div className="docs__upload-actions">
              <button type="submit" className="btn btn--primary" disabled={!selectedFile || uploading}>
                {uploading ? "Uploading & indexing…" : "Upload & index"}
              </button>
              {selectedFile && !uploading && (
                <button type="button" className="btn btn--sm" onClick={() => pickFile(null)}>Clear</button>
              )}
            </div>

            {uploadNotice && <div className="docs__alert docs__alert--ok">{uploadNotice}</div>}
            {uploadError && <div className="docs__alert docs__alert--error" role="alert">{uploadError}</div>}
          </form>
        </section>
      </div>

      {documentId && (
        <div ref={inspectorRef}>
          {validDocId ? (
            <DocumentInspector
              key={documentId}
              id={documentId}
              onClose={() => { window.location.hash = "/documents"; }}
              onReindexed={() => void loadStats()}
            />
          ) : (
            <div className="docs__alert docs__alert--error" role="alert">Invalid document link.</div>
          )}
        </div>
      )}
    </div>
  );
}
