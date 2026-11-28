import { useEffect, useRef, useState, type FormEvent } from "react";
import { api, ApiError, apiMode, describeProblem } from "../api/client";
import { ACCEPT_ATTRIBUTE } from "../api/types";
import { SemanticSearchPanel } from "./SemanticSearchPanel";
import { DocumentReading } from "./DocumentReading";

/** Submit a selected file for OCR or local storage, then show its receipt.
 * @param props - Component inputs.
 * @param props.documentId - Receipt from the route, empty before upload.
 * @param props.page - Optional original page linked from an indexed excerpt.
 * @param props.canUpload - Whether the current account may submit files.
 */
export function Documents({ documentId, page, canUpload }: { documentId: string; page?: number; canUpload: boolean }) {
  const [submission, setSubmission] = useState<{ file: File; localKey: string; ocrKey: string }>();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const mounted = useRef(false);
  const form = useRef<HTMLFormElement>(null);
  useEffect(() => {
    mounted.current = true;
    return () => { mounted.current = false; };
  }, []);

  /** Select a file without transmitting it and retain independent keys for each submission mode. */
  function selectFile(file?: File) {
    if (documentId) window.location.hash = "/documents";
    setSubmission(file ? { file, localKey: crypto.randomUUID(), ocrKey: crypto.randomUUID() } : undefined);
    setError("");
  }

  /** Submit the selected file and navigate to its receipt without losing the retry key on network failure.
   * @param event - Upload form submission.
   */
  async function upload(event: FormEvent) {
    event.preventDefault();
    if (!canUpload || !submission || busy) return;
    const sender = (event.nativeEvent as SubmitEvent).submitter;
    const ocr = apiMode === "http" && (!(sender instanceof HTMLButtonElement) || sender.value !== "local");
    setBusy(true);
    setError("");
    try {
      const { id } = await api.upload(submission.file, ocr ? submission.ocrKey : submission.localKey, ocr);
      if (!mounted.current) return;
      window.location.hash = "/documents/" + encodeURIComponent(id);
      setSubmission(undefined);
      form.current?.reset();
    } catch (err) {
      if (!mounted.current) return;
      if (err instanceof ApiError && err.problem.code === "ocr_disabled") {
        setError(describeProblem(err.problem.code));
      } else if (err instanceof ApiError && err.problem.status >= 400 && err.problem.status < 500) {
        setError(describeProblem(err.problem.code));
        setSubmission(undefined);
        form.current?.reset();
      } else {
        setError("Receipt not confirmed. Retry this submission to reuse the same file and key.");
      }
    } finally {
      if (mounted.current) setBusy(false);
    }
  }

  return (
    <section className="docs">
      <h1>Documents</h1>
      <p className="notice">{!canUpload
        ? "Read-only access. Open a receipt link or search indexed OCR documents."
        : apiMode === "http"
          ? "Local API · Uploads are saved privately. Send to OCR transcribes the document with SPUR, one printed page at a time. When enabled, semantic indexing uses the readable transcript. Selecting a file sends nothing."
          : "Mock mode · Results are invented, kept in this page session and lost on reload."}</p>
      {apiMode === "http" && <SemanticSearchPanel />}
      {canUpload && <form className="panel" onSubmit={upload} ref={form}>
        <div className="docs__dropzone" onDragOver={(e) => e.preventDefault()} onDrop={(e) => {
          e.preventDefault();
          if (!busy && e.dataTransfer.files.length === 1) {
            form.current?.reset();
            selectFile(e.dataTransfer.files[0]);
          }
        }}>
          <label htmlFor="document-file">Select a document</label>
          <input id="document-file" type="file" accept={apiMode === "http" ? undefined : ACCEPT_ATTRIBUTE} disabled={busy}
            onChange={(e) => selectFile(e.target.files?.[0])} />
          <span>{submission?.file.name ?? "Or drop one file here."}</span>
        </div>
        {apiMode === "http" && <>
          <p>PDF, DOCX, XLSX, PPTX, UTF-8 TXT/CSV (up to 20 MiB and 20 printed pages), or PNG/JPEG (4 MiB).
            For other formats, export to PDF first.</p>
          <button type="submit" className="btn btn--primary" value="ocr" disabled={!submission || busy}>
            {busy ? "Uploading…" : "Send to OCR"}
          </button>
        </>}
        {(apiMode !== "http" || !submission || /\.(pdf|xlsx|csv)$/i.test(submission.file.name)) &&
          <button type="submit" className="btn" value="local" disabled={!submission || busy}>
            {apiMode === "http" ? "Upload document locally" : "Upload document"}
          </button>}
        {error && <p role="alert">{error}</p>}
      </form>}
      {documentId && (/^[\da-f]{8}(?:-[\da-f]{4}){3}-[\da-f]{12}$/i.test(documentId)
        ? <DocumentReading key={documentId} id={documentId} page={page} canReindex={canUpload} />
        : <p role="alert">Invalid document link.</p>)}
    </section>
  );
}
