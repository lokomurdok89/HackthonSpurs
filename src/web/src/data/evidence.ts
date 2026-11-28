import type { BomLine, EvidenceType } from "./types";

export interface EvidenceMeta {
  label: string;
  /** Bar and swatch color. Colors also differ in lightness so they stay distinguishable. */
  color: string;
  chipBg: string;
  chipFg: string;
}

/** Colors come from styles/theme.css so they follow the light or dark theme. */
function tokens(key: string) {
  return { color: `var(--ev-${key})`, chipBg: `var(--ev-${key}-bg)`, chipFg: `var(--ev-${key}-fg)` };
}

export const EVIDENCE: Record<EvidenceType, EvidenceMeta> = {
  unknown_origin: { label: "Unknown origin", ...tokens("unknown") },
  invoice_states_origin: { label: "Invoice states origin", ...tokens("invoice") },
  lot_specific_mill_test_report: { label: "Lot-specific mill test report", ...tokens("mtr") },
  lot_dependent: { label: "Lot-dependent", ...tokens("lot") },
  supplier_origin_certification: { label: "Supplier certification", ...tokens("cert") },
  purchase_without_origin: { label: "Purchase, no origin stated", ...tokens("none") },
};

const CUSMA_COUNTRIES = new Set(["CA", "US", "MX"]);
/** Evidence types strong enough to count a CUSMA-region country as "evidenced". */
const STRONG: ReadonlySet<EvidenceType> = new Set(["lot_specific_mill_test_report", "supplier_origin_certification"]);

export type OriginBucket = "evidenced_cusma" | "stated_outside" | "unproven";

/**
 * Sorts a BOM line into one of three buckets for the summary cards.
 * This is a view of the evidence, not a regional value content calculation.
 */
export function bucketOf(line: BomLine): OriginBucket {
  const c = line.evidencedCountry;
  if (c && CUSMA_COUNTRIES.has(c) && STRONG.has(line.evidence)) return "evidenced_cusma";
  if (c && !CUSMA_COUNTRIES.has(c)) return "stated_outside";
  return "unproven";
}

export function summarize(bom: BomLine[]) {
  const total = bom.reduce((s, l) => s + l.costCad, 0);
  const byBucket: Record<OriginBucket, number> = { evidenced_cusma: 0, stated_outside: 0, unproven: 0 };
  const byType = new Map<EvidenceType, number>();
  for (const l of bom) {
    byBucket[bucketOf(l)] += l.costCad;
    byType.set(l.evidence, (byType.get(l.evidence) ?? 0) + l.costCad);
  }
  const mix = [...byType.entries()]
    .map(([type, amount]) => ({ type, amount, share: total ? amount / total : 0 }))
    .sort((a, b) => b.amount - a.amount);
  const largestUnknown = bom
    .filter((l) => bucketOf(l) === "unproven")
    .sort((a, b) => b.costCad - a.costCad)[0];
  return { total, byBucket, mix, largestUnknown };
}

/** The document that would prove a not-proven line, if it confirms CA, US or MX origin. */
const NEEDED: Partial<Record<EvidenceType, string>> = {
  unknown_origin: "Origin declaration naming the manufacturer",
  lot_dependent: "Mill test report for each lot used",
  purchase_without_origin: "Supplier origin certification",
  invoice_states_origin: "Supplier origin certification",
};

export function neededDocument(line: BomLine): string | undefined {
  return bucketOf(line) === "unproven" ? (NEEDED[line.evidence] ?? "Supplier origin certification") : undefined;
}

export interface SupplierSummary {
  supplier: string;
  lines: BomLine[];
  total: number;
  byBucket: Record<OriginBucket, number>;
  /** Not-proven lines from this supplier with the document to ask for. */
  asks: { line: BomLine; document: string }[];
}

/** Groups the BOM by supplier, the suppliers with the most unproven cost first. */
export function bySupplier(bom: BomLine[]): SupplierSummary[] {
  const map = new Map<string, SupplierSummary>();
  for (const l of bom) {
    let s = map.get(l.supplier);
    if (!s) {
      s = { supplier: l.supplier, lines: [], total: 0, byBucket: { evidenced_cusma: 0, stated_outside: 0, unproven: 0 }, asks: [] };
      map.set(l.supplier, s);
    }
    s.lines.push(l);
    s.total += l.costCad;
    s.byBucket[bucketOf(l)] += l.costCad;
    const document = neededDocument(l);
    if (document) s.asks.push({ line: l, document });
  }
  return [...map.values()].sort((a, b) => b.byBucket.unproven - a.byBucket.unproven || b.total - a.total);
}

export const cad = (n: number) => `CAD ${n.toFixed(2)}`;
export const pct = (share: number) => `${(share * 100).toFixed(1)}%`;
