import { useEffect, useState } from "react";
import { ApiError } from "../api/errors";
import { getIndexStatus } from "../api/search";
import type { DocumentIndexStatus } from "../api/types";

const PENDING = ["AwaitingReading", "Queued", "Processing"];

/** Describe each index state separately from the OCR outcome.
 * @param status - Latest durable index status for this receipt.
 */
function indexMessage(status: DocumentIndexStatus): string {
  switch (status.state) {
    case "AwaitingReading": return "Waiting for OCR to publish a reading.";
    case "Queued": return "Queued for semantic indexing.";
    case "Processing": return "Indexing the OCR transcript.";
    case "Completed": return `Indexed · ${status.chunkCount} excerpts.`;
    case "Skipped": return "Skipped: no readable text was returned.";
    case "NotApplicable": return "Only successful SPUR readings are eligible.";
    case "NotIndexed": return "This reading was published before automatic indexing.";
    case "Failed": return "Indexing failed. The OCR reading is still available.";
  }
}

/** Poll and show the index while keeping the reading independently available.
 * @param props - Component inputs.
 * @param props.id - Current receipt identifier.
 */
export function DocumentIndex({ id }: { id: string }) {
  const [status, setStatus] = useState<DocumentIndexStatus>();
  const [unavailable, setUnavailable] = useState(false);
  useEffect(() => {
    const controller = new AbortController();
    const { signal } = controller;
    let timer: ReturnType<typeof setTimeout>;
    /** Check the index and poll only while scheduled or recovering from a temporary failure. */
    async function check() {
      try {
        const current = await getIndexStatus(id, signal);
        if (signal.aborted) return;
        setStatus(current);
        setUnavailable(false);
        if (PENDING.includes(current.state)) timer = setTimeout(check, 2000);
      } catch (error) {
        if (signal.aborted) return;
        setUnavailable(true);
        if (!(error instanceof ApiError) || error.problem.status >= 500) timer = setTimeout(check, 8000);
      }
    }
    void check();
    return () => { controller.abort(); clearTimeout(timer); };
  }, [id]);

  if (status?.state === "NotApplicable" || (!status && !unavailable)) return null;
  return (
    <div aria-live="polite">
      <h3>Semantic index</h3>
      {unavailable ? <p>Index status is currently unavailable.</p> : status && <p>{indexMessage(status)}</p>}
      {status?.state === "Completed" && <a href={`#/search/${id}`}>Search this document</a>}
    </div>
  );
}
