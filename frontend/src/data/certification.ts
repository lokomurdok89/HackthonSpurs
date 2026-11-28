import { bucketOf } from "./evidence";
import type { ProductEvidence } from "./types";

// Items follow the minimum data elements of CUSMA Annex 5-A. The ninth, the signed and
// dated certifying statement, is completed on the exported draft rather than in the app.

export type ItemKey = "role" | "certifier" | "exporter" | "producer" | "importer" | "goods" | "criterion" | "period";
export type ItemStatus = "ready" | "review" | "missing";

export interface Entry {
  value: string;
  /** A person has checked this value. Any change clears it. */
  confirmed: boolean;
}

export type Draft = Record<ItemKey, Entry>;

export interface Option {
  value: string;
  /** Plain explanation shown under the menu once chosen. */
  detail?: string;
}

export interface ItemSpec {
  key: ItemKey;
  label: string;
  help: string;
  options?: Option[];
}

/** Facts from the product record that the checks compare against. */
export interface CertContext {
  productId: string;
  prefill: Draft;
  /** Components whose CUSMA origin is not proven, with their share of material cost. */
  unproven: { ids: string[]; pct: number };
  /** HS codes listed for components, which must not stand in for the finished good. */
  componentHs: Map<string, string>;
}

// Letters are for reference only; the treaty labels these paragraphs (a) to (d) of Article 4.2.
export const CRITERIA: Option[] = [
  { value: "A: wholly obtained or produced", detail: "Article 4.2(a). Wholly obtained or produced in Canada, the US or Mexico, as defined in Article 4.3, such as minerals mined or crops grown there." },
  { value: "B: meets the product-specific rule", detail: "Article 4.2(b). Made in the region using some non-originating materials, and meets every applicable rule for its HS code in Annex 4-B." },
  { value: "C: made only from originating materials", detail: "Article 4.2(c). Made in the region exclusively from materials that are themselves originating." },
  { value: "D: regional value content fallback", detail: "Article 4.2(d). Not for textiles and apparel (HS chapters 61 to 63). Used when a non-originating part cannot meet the product rule only because it shares the good's HS classification. Regional value content must be at least 60% by transaction value or 50% by net cost." },
];

export const ITEMS: ItemSpec[] = [
  { key: "role", label: "Who is certifying", help: "The importer, exporter or producer.", options: [{ value: "Exporter" }, { value: "Producer" }, { value: "Importer" }] },
  { key: "certifier", label: "Certifier details", help: "Name, title, address with country, phone and email." },
  { key: "exporter", label: "Exporter", help: "Name, address with country, phone and email. Enter Unknown if the producer certifies and does not know." },
  { key: "producer", label: "Producer", help: "Name, address with country, phone and email. Various or Unknown are also accepted." },
  { key: "importer", label: "Importer", help: "The importer of record, which may not be your customer. Name, address, phone and email, or Unknown." },
  { key: "goods", label: "Description and HS code", help: "Describe the goods and include the 6-digit HS code of the finished good, for example 1234.56." },
  { key: "criterion", label: "Origin criterion", help: "A qualified person chooses this. The app does not suggest one.", options: CRITERIA },
  { key: "period", label: "Shipments covered", help: "One shipment, with its invoice number if known, or identical goods over up to 12 months." },
];

export const SINGLE_SHIPMENT = "Single shipment";
const PLACEHOLDERS = /^(unknown|various)$/i;

/** Read the period entry: one shipment with an optional invoice number, or a blanket date range. */
export function parsePeriod(value: string) {
  if (value.startsWith(SINGLE_SHIPMENT)) return { kind: "single" as const, invoice: value.split(", invoice ")[1] ?? "" };
  if (value.includes(" to ")) {
    const [from = "", to = ""] = value.split(" to ");
    return { kind: "blanket" as const, from, to };
  }
  return { kind: "none" as const };
}

export function contextFor(product: ProductEvidence, manufacturer: string): CertContext {
  const total = product.bom.reduce((s, l) => s + l.costCad, 0);
  const open = product.bom.filter((l) => bucketOf(l) !== "evidenced_cusma");
  const openCost = open.reduce((s, l) => s + l.costCad, 0);
  return {
    productId: product.productId,
    prefill: newDraft(product, manufacturer),
    unproven: { ids: open.map((l) => l.componentId), pct: total ? (openCost / total) * 100 : 0 },
    componentHs: new Map(product.bom.map((l) => [l.suggestedHs.replace(".", ""), l.componentId])),
  };
}

/** Start a draft, prefilling only what the product record states. Prefilled values still need confirmation. */
export function newDraft(product: ProductEvidence, manufacturer: string): Draft {
  const draft = Object.fromEntries(ITEMS.map((i) => [i.key, { value: "", confirmed: false }])) as Draft;
  draft.producer.value = manufacturer;
  draft.importer.value = product.customer;
  draft.goods.value = `${product.productId} ${product.description}`;
  return draft;
}

/** Explain what blocks a value from being confirmed, or return an empty string. */
export function problemWith(key: ItemKey, value: string, ctx: CertContext) {
  if (!value.trim()) return "Enter a value first.";
  const isParty = key === "certifier" || key === "exporter" || key === "producer" || key === "importer";
  if (isParty && !PLACEHOLDERS.test(value.trim()) && value.trim() === ctx.prefill[key].value) {
    return "Only a name is filled in. Add the address with country, phone and email.";
  }
  if (key === "goods") {
    const hs = value.match(/(?:^|\D)(\d{4})\.?(\d{2})(?!\d)/);
    if (!hs) return "Add the 6-digit HS code of the finished good.";
    const part = ctx.componentHs.get(hs[1] + hs[2]);
    if (part) return `${hs[1]}.${hs[2]} is the code listed for component ${part}. Use the code for the finished good.`;
  }
  if (key === "criterion" && value.startsWith("C:") && ctx.unproven.ids.length) {
    return `Criterion C needs every material to be originating, but ${ctx.unproven.ids.join(", ")} (${ctx.unproven.pct.toFixed(1)}% of material cost) has no proof of CUSMA origin.`;
  }
  if (key === "period") {
    const p = parsePeriod(value);
    if (p.kind === "none") return "Choose one shipment or a blanket period.";
    if (p.kind === "blanket") {
      if (!p.from || !p.to) return "Choose both dates.";
      if (p.to < p.from) return "The end date is before the start date.";
      const limit = new Date(`${p.from}T00:00:00Z`);
      limit.setUTCFullYear(limit.getUTCFullYear() + 1);
      if (new Date(`${p.to}T00:00:00Z`) >= limit) return "A blanket period cannot exceed 12 months.";
    }
  }
  return "";
}

export function statusOf(key: ItemKey, entry: Entry, ctx: CertContext): ItemStatus {
  if (!entry.value.trim()) return "missing";
  return entry.confirmed && !problemWith(key, entry.value, ctx) ? "ready" : "review";
}

export function draftText(productId: string, draft: Draft) {
  const shown = (key: ItemKey) => {
    const v = draft[key].value;
    if (key === "criterion") return CRITERIA.find((c) => c.value === v)?.detail?.split(". ")[0] + `, ${v}`;
    if (key === "period" && parsePeriod(v).kind === "single" && !v.includes(", invoice ")) return `${v}, invoice number unknown`;
    return v;
  };
  const lines = ITEMS.map((i, n) => `${n + 1}. ${i.label}\n${shown(i.key)}`);
  return [
    `DRAFT CUSMA certification of origin, ${productId}`,
    "Not signed. Not a determination that the goods qualify.",
    ...lines,
    "9. Certifying statement, signature and date\nAdd the statement required by Annex 5-A, then sign and date.",
    "Keep this certification and its supporting records for at least five years.",
  ].join("\n\n");
}

const storageKey = (productId: string) => `origin-vault:certification:${productId}`;

/** Load a saved draft, dropping choices that are no longer offered. */
export function loadDraft(ctx: CertContext): Draft {
  try {
    const raw = localStorage.getItem(storageKey(ctx.productId));
    if (!raw) return ctx.prefill;
    const draft = { ...ctx.prefill, ...(JSON.parse(raw) as Partial<Draft>) };
    for (const item of ITEMS) {
      if (item.options && draft[item.key].value && !item.options.some((o) => o.value === draft[item.key].value)) {
        draft[item.key] = { value: "", confirmed: false };
      }
    }
    return draft;
  } catch {
    return ctx.prefill;
  }
}

export function saveDraft(productId: string, draft: Draft) {
  try {
    localStorage.setItem(storageKey(productId), JSON.stringify(draft));
  } catch {
    /* Storage blocked: the draft lasts until the page changes. */
  }
}
