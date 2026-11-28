import type { EvidenceType, SourceRef } from "../data/types";
import { EVIDENCE } from "../data/evidence";

/** Render citation number n for the source and invoke the supplied source opener on activation. */
export function Cite({ n, source, onOpen }: { n: number; source: SourceRef; onOpen: (n: number) => void }) {
  return (
    <button
      type="button"
      className="cite"
      onClick={() => onOpen(n)}
      title={`${source.file} · ${source.where}`}
      aria-label={`Source ${n}: ${source.file}, ${source.where}`}
    >
      {n}
    </button>
  );
}

/** Render the evidence type and optional country code as a compact status stamp. */
export function Stamp({ type, country }: { type: EvidenceType; country?: string }) {
  const meta = EVIDENCE[type];
  return (
    <span className="stamp" style={{ color: meta.chipFg }}>
      {meta.label}
      {country ? ` (${country})` : ""}
    </span>
  );
}
