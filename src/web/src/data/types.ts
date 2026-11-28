// View models for the evidence screens. These are UI shapes, not a backend contract yet.

export type EvidenceType =
  | "lot_specific_mill_test_report"
  | "supplier_origin_certification"
  | "invoice_states_origin"
  | "lot_dependent"
  | "purchase_without_origin"
  | "unknown_origin";

export interface SourceRef {
  file: string;
  /** Human-readable locator: "p.1", "Cover!A9", "row 5". */
  where: string;
}

export interface BomLine {
  componentId: string;
  description: string;
  supplier: string;
  originPerBom: string;
  suggestedHs: string;
  evidence: EvidenceType;
  evidencedCountry?: string;
  note: string;
  costCad: number;
  source: SourceRef;
}

export interface Finding {
  code: string;
  topic: string;
  severity: "high" | "medium";
  title: string;
  body: string;
  sources: SourceRef[];
  nextStep: string;
}

export interface ProductEvidence {
  productId: string;
  description: string;
  customer: string;
  customerPart: string;
  drawing: string;
  bom: BomLine[];
  findings: Finding[];
}

export type ReviewStatus = "pending" | "approved" | "corrected" | "rejected";

export interface ExtractedField {
  id: string;
  label: string;
  value: string;
  /** Original-language quote the value came from. */
  quote?: string;
  translation?: string;
  confidence: number;
  where: string;
}

export interface ReviewItem {
  id: string;
  file: string;
  docType: string;
  supplier: string;
  language: "en" | "fr";
  method: "native" | "ocr";
  /** Lines shown in the source preview; `hit` marks the field it supports. */
  previewLines: { text: string; hit?: string }[];
  fields: ExtractedField[];
}

export interface PassportLot {
  lot: string;
  form: string;
  recycledPct?: number;
  co2PerKg?: number;
  castingCountry?: string;
  source: SourceRef;
}

export interface PassportField {
  label: string;
  value: string;
  status: "verified" | "pending" | "missing" | "flagged";
  note?: string;
  source?: SourceRef;
}

export interface Passport {
  productId: string;
  name: string;
  manufacturer: string;
  fields: PassportField[];
  lots: PassportLot[];
  allocationNote: string;
}

export interface DemoData {
  label: string;
  product: ProductEvidence;
  review: ReviewItem[];
  passport: Passport;
}
