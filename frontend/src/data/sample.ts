import type { DemoData } from "./types";

// Invented placeholder data, safe to commit. The real demo data lives in a
// git-ignored *.local.ts file (see data/index.ts) because the event dataset
// must not be published in this public repository.

export const sampleData: DemoData = {
  label: "Synthetic sample",
  product: {
    productId: "EX-100",
    description: "Example welded bracket",
    customer: "Example Buyer Inc.",
    customerPart: "EB-0001",
    drawing: "EX-100-D01 Rev A",
    bom: [
      {
        componentId: "CAS-014", description: "Bushing, machined", supplier: "Example Supplier",
        originPerBom: "Canada", suggestedHs: "8483.30", evidence: "unknown_origin",
        note: "Certificate CERT-027 gives the grade and an address, not an origin declaration.",
        costCad: 12.5, source: { file: "CERT-027.pdf", where: "p.1" },
      },
      {
        componentId: "PL-010", description: "Steel plate 10 mm", supplier: "Example Mill",
        originPerBom: "Canada", suggestedHs: "7208.51", evidence: "lot_specific_mill_test_report",
        evidencedCountry: "CA", note: "Covers this heat only.",
        costCad: 4.2, source: { file: "MTR-0001.pdf", where: "p.1" },
      },
    ],
    findings: [
      {
        code: "X01", topic: "Origin", severity: "high",
        title: "Certificate does not demonstrate origin",
        body: "The part and certificate IDs match, but the certificate shows only a grade and supplier address.",
        sources: [{ file: "Materials.xlsx", where: "Materiales!A12" }, { file: "CERT-027.pdf", where: "p.1" }],
        nextStep: "Request origin evidence from the supplier for this part.",
      },
    ],
  },
  review: [
    {
      id: "r1", file: "CERT-027.pdf", docType: "Supplier certificate", supplier: "Example Supplier",
      language: "en", method: "native",
      previewLines: [
        { text: "CERTIFICATE OF CONFORMANCE" },
        { text: "Certificate no. CERT-027", hit: "f1" },
        { text: "Part CAS-014 · Bushing, machined", hit: "f2" },
        { text: "Grade 4140, hardened", hit: "f3" },
        { text: "100 Example Rd, Example City" },
      ],
      fields: [
        { id: "f1", label: "Certificate number", value: "CERT-027", confidence: 0.98, where: "p.1" },
        { id: "f2", label: "Part number", value: "CAS-014", confidence: 0.96, where: "p.1" },
        { id: "f3", label: "Material grade", value: "4140", confidence: 0.91, where: "p.1" },
      ],
    },
  ],
  passport: {
    productId: "EX-100", name: "Example welded bracket", manufacturer: "Example Manufacturer Inc.",
    fields: [
      { label: "Nominal mass", value: "1.00 kg", status: "verified", source: { file: "EX-100-D01.pdf", where: "p.1" } },
      { label: "Supplier ISO 9001", value: "Not demonstrated", status: "missing" },
    ],
    lots: [
      { lot: "L-001", form: "plate", recycledPct: 25, co2PerKg: 2.0, source: { file: "MTR-0001.pdf", where: "p.1" } },
    ],
    allocationNote: "No lot has been allocated to this order yet.",
  },
};
