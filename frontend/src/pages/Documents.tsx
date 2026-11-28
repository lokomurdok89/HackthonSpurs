import { useEffect, useRef, useState, type FormEvent } from "react";
import { api, ApiError, apiMode, describeProblem } from "../api/client";
import { ACCEPT_ATTRIBUTE } from "../api/types";
import { DocumentReading } from "./DocumentReading";

export function Documents({ documentId }: { documentId: string }) {
  const [submission, setSubmission] = useState<{ file: File; key: string }>();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const mounted = useRef(false);
  const form = useRef<HTMLFormElement>(null);
  useEffect(() => {
    mounted.current = true;
    return () => { mounted.current = false; };
  }, []);

  async function upload(event: FormEvent) {
    event.preventDefault();
    if (!submission || busy) return;
    setBusy(true);
    setError("");
    try {
      const { id } = await api.upload(submission.file, submission.key);
      if (!mounted.current) return;
      window.location.hash = "/documents/" + encodeURIComponent(id);
      setSubmission(undefined);
      form.current?.reset();
    } catch (err) {
      if (!mounted.current) return;
      if (err instanceof ApiError && err.problem.status >= 400 && err.problem.status < 500) {
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
    <section>
      <h1>Documents</h1>
      <p className="notice">{apiMode === "http"
        ? "Local API · Uploads are saved privately. Only the synthetic Worker profile processes the demo CSV; other files stay queued."
        : "Mock mode · Results are invented, kept in this page session and lost on reload."}</p>
      <form className="panel" onSubmit={upload} ref={form}>
        <label htmlFor="document-file">Select a PDF, XLSX or CSV file</label>{" "}
        <input id="document-file" type="file" accept={ACCEPT_ATTRIBUTE} disabled={busy}
          onChange={(e) => {
            const file = e.target.files?.[0];
            setSubmission(file ? { file, key: crypto.randomUUID() } : undefined);
            setError("");
          }} />
        <button className="btn btn--primary" disabled={!submission || busy}>
          {busy ? "Uploading…" : "Upload document"}
        </button>
        {error && <p role="alert">{error}</p>}
      </form>
      {documentId && (/^[\da-f]{8}(?:-[\da-f]{4}){3}-[\da-f]{12}$/i.test(documentId)
        ? <DocumentReading key={documentId} id={documentId} />
        : <p role="alert">Invalid document link.</p>)}
    </section>
  );
}
