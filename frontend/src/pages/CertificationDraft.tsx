import { useState } from "react";
import { contextFor, draftText, ITEMS, loadDraft, saveDraft, statusOf, type Entry, type ItemKey } from "../data/certification";
import { logActivity } from "../data/settings";
import type { ProductEvidence } from "../data/types";
import { CertificationItem } from "./CertificationItem";
import "./CertificationDraft.css";

interface Props {
  product: ProductEvidence;
  manufacturer: string;
  canEdit: boolean;
  onOpenSuppliers: () => void;
}

/** Checklist of the certification data elements for one product, with an unsigned draft export. */
export function CertificationDraft({ product, manufacturer, canEdit, onOpenSuppliers }: Props) {
  const ctx = contextFor(product, manufacturer);
  const [draft, setDraft] = useState(() => loadDraft(ctx));
  const ready = ITEMS.filter((i) => statusOf(i.key, draft[i.key], ctx) === "ready").length;
  const unproven = ctx.unproven.pct;
  const source: Partial<Record<ItemKey, string>> = {
    producer: "passport manufacturer",
    importer: "customer on the product record. Check they are the importer of record",
    goods: "product record",
  };

  const change = (key: ItemKey, entry: Entry) => {
    const label = ITEMS.find((i) => i.key === key)?.label ?? key;
    if (entry.confirmed && !draft[key].confirmed) logActivity("Confirmed certification item", `${label} for ${product.productId}`);
    const next = { ...draft, [key]: entry };
    setDraft(next);
    saveDraft(product.productId, next);
  };

  const download = () => {
    const a = document.createElement("a");
    a.href = URL.createObjectURL(new Blob([draftText(product.productId, draft)], { type: "text/plain" }));
    a.download = `${product.productId}-certification-draft.txt`;
    a.click();
    URL.revokeObjectURL(a.href);
    logActivity("Exported certification draft", product.productId);
  };

  return (
    <div className="cert">
      <div className="cert__bar">
        <p className="cert__count" role="status"><strong>{ready} of {ITEMS.length}</strong> items filled</p>
        {canEdit ? (
          <button type="button" className="btn btn--primary" disabled={ready < ITEMS.length} onClick={download}>
            Export draft
          </button>
        ) : <span className="muted small">Only administrators can edit or export.</span>}
      </div>
      <progress className="cert__progress" max={ITEMS.length} value={ready} aria-label="Items filled" />

      {unproven > 0.05 || product.findings.length ? (
        <div className="notice cert__warning">
          <p>
            A filled form does not mean the product qualifies. {unproven.toFixed(1)}% of material cost has no proof of
            CUSMA origin, and {product.findings.length} {product.findings.length === 1 ? "finding is" : "findings are"} open.
          </p>
          <button type="button" className="btn btn--sm" onClick={onOpenSuppliers}>See supplier declarations</button>
        </div>
      ) : (
        <p className="notice">A filled form does not mean the product qualifies.</p>
      )}

      <ol className="cert__list">
        {ITEMS.map((item) => (
          <CertificationItem key={item.key} item={item} entry={draft[item.key]} ctx={ctx} canEdit={canEdit}
            source={draft[item.key].value && draft[item.key].value === ctx.prefill[item.key].value ? source[item.key] : undefined}
            onChange={(entry) => change(item.key, entry)} />
        ))}
      </ol>
      <p className="muted small">
        The certifying statement, signature and date are added to the exported draft by the person certifying. Keep
        records for at least five years.
      </p>
    </div>
  );
}
