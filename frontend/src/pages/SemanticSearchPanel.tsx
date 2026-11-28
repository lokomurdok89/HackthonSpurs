import { useState } from "react";
import { DocumentsSearch } from "./DocumentsSearch";

/** Open the shared authenticated search from Documents without starting a second upload or query. */
export function SemanticSearchPanel() {
  const [open, setOpen] = useState(false);
  return <section className="panel">
    <button type="button" className="btn" aria-expanded={open} onClick={() => setOpen(!open)}>Search indexed documents</button>
    {open && <DocumentsSearch documentId="" embedded />}
  </section>;
}
