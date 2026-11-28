import type { BomLine, EvidenceType, Finding, SourceRef } from "./types";

export interface EvidenceMeta {
  label: string;
  /** Bar and swatch color. Colors also differ in lightness so they stay distinguishable. */
  color: string;
  chipBg: string;
  chipFg: string;
}

/** Find the theme color variables for the given evidence style key. */
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

/** Group the provided BOM line by technical origin evidence; this is not a regional value content calculation. */
export function bucketOf(line: BomLine): OriginBucket {
  const c = line.evidencedCountry;
  if (c && CUSMA_COUNTRIES.has(c) && STRONG.has(line.evidence)) return "evidenced_cusma";
  if (c && !CUSMA_COUNTRIES.has(c)) return "stated_outside";
  return "unproven";
}

/** Summarize the given BOM by cost, origin evidence and its highest-cost unresolved line. */
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

/** Describe the evidence still needed to support the supplied BOM line, if any. */
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

/** Group BOM lines by supplier and order suppliers by highest unresolved cost. */
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

/** Format the numeric amount in CAD with two decimal places. */
export const cad = (n: number) => `CAD ${n.toFixed(2)}`;
/** Format the supplied proportional share as a percentage. */
export const pct = (share: number) => `${(share * 100).toFixed(1)}%`;

/** Assign stable reading-order references to all sources in the BOM and findings. */
export function numberSources(bom: BomLine[], findings: Finding[]) {
  const list: SourceRef[] = [];
  const index = new Map<string, number>();
  /** Use the supplied source's filename and locator to identify a cited position. */
  const key = (s: SourceRef) => `${s.file}|${s.where}`;
  /** Assign the supplied source the next number unless its citation already exists. */
  const add = (s: SourceRef) => {
    if (!index.has(key(s))) {
      list.push(s);
      index.set(key(s), list.length);
    }
  };
  bom.forEach((l) => add(l.source));
  findings.forEach((f) => f.sources.forEach(add));
  return { list, numberOf: (s: SourceRef) => index.get(key(s))! };
}
