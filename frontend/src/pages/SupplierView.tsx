import { useState } from "react";
import type { BomLine } from "../data/types";
import { bySupplier, type SupplierSummary } from "../data/evidence";

/** Draft an unsent evidence request based on the supplier's missing documents and the given product ID. */
function draftText(s: SupplierSummary, productId: string) {
  const items = s.asks.map((a) => `- ${a.document} for ${a.line.description} (our part ${a.line.componentId})`);
  return [
    `Hello ${s.supplier.replace(/\s*\(.*\)$/, "")} team,`,
    "",
    `We are preparing a CUSMA certification of origin for our product ${productId}. To support it, could you send us:`,
    "",
    ...items,
    "",
    "If the country of origin is outside Canada, the US or Mexico, please say so; that is useful to us as well.",
    "",
    "Thank you,",
  ].join("\n");
}

/** Show a copyable evidence request for the supplied supplier summary and product identifier. */
function Draft({ s, productId }: { s: SupplierSummary; productId: string }) {
  const text = draftText(s, productId);
  const fieldId = `draft-text-${s.supplier.replace(/\W+/g, "-")}`;
  const [copied, setCopied] = useState(false);
  /** Copy the displayed request text and show temporary confirmation. */
  const copy = async () => {
    try {
      await navigator.clipboard.writeText(text);
      setCopied(true);
      window.setTimeout(() => setCopied(false), 2000);
    } catch {
      setCopied(false);
    }
  };
  return (
    <div className="draft">
      <label className="draft__label" htmlFor={fieldId}>
        Request to {s.supplier}
      </label>
      <textarea id={fieldId} readOnly value={text} rows={text.split("\n").length} />
      <div className="draft__actions">
        <button type="button" className="btn btn--sm btn--primary" onClick={copy}>
          Copy text
        </button>
        <span className="small muted" role="status">
          {copied ? "Copied" : ""}
        </span>
      </div>
    </div>
  );
}

/** Group the given BOM by supplier and show evidence requests for the supplied product ID. */
export function SupplierView({ bom, productId }: { bom: BomLine[]; productId: string }) {
  const suppliers = bySupplier(bom);
  const [open, setOpen] = useState<string | null>(null);

  return (
    <section className="suppliers" id="suppliers" aria-labelledby="sup-title">
      <h2 id="sup-title" className="visually-hidden">
        Supplier origin declarations
      </h2>
      <div className="table-scroll">
        <table className="sup">
          <thead>
            <tr>
              <th scope="col">Supplier</th>
              <th scope="col" className="num hide-sm">
                Lines
              </th>
              <th scope="col" className="num">
                CAD / unit
              </th>
              <th scope="col" className="num">
                Proven
              </th>
              <th scope="col" className="num hide-sm">
                Outside
              </th>
              <th scope="col" className="num">
                Not proven
              </th>
              <th scope="col" className="hide-sm">Ask for</th>
              <th scope="col" className="hide-sm">
                <span className="visually-hidden">Request</span>
              </th>
            </tr>
          </thead>
          <tbody>
            {suppliers.map((s) => {
              const isOpen = open === s.supplier;
              const docs = [...new Set(s.asks.map((a) => a.document))];
              return (
                <SupplierRows
                  key={s.supplier}
                  s={s}
                  docs={docs}
                  isOpen={isOpen}
                  onToggle={() => setOpen(isOpen ? null : s.supplier)}
                  productId={productId}
                />
              );
            })}
          </tbody>
        </table>
      </div>
    </section>
  );
}

interface RowsProps {
  s: SupplierSummary;
  docs: string[];
  isOpen: boolean;
  onToggle: () => void;
  productId: string;
}

/** Render a supplier summary with required documents; use the given expanded state, toggle callback and product ID for details. */
function SupplierRows({ s, docs, isOpen, onToggle, productId }: RowsProps) {
  /** Show the given amount as a fraction of the current supplier total. */
  const share = (n: number) => `${(n / s.total) * 100}%`;
  const draftId = `draft-row-${s.supplier.replace(/\W+/g, "-")}`;
  const asks = docs.length ? (
    <ul className="sup__docs">
      {docs.map((d) => (
        <li key={d}>{d}</li>
      ))}
    </ul>
  ) : (
    <span className="muted">{s.byBucket.stated_outside ? "Stated outside CUSMA" : "Proven"}</span>
  );
  const button = docs.length ? (
    <button type="button" className="btn btn--sm" aria-expanded={isOpen} aria-controls={draftId} onClick={onToggle}>
      {isOpen ? "Hide request" : "Draft request"}
    </button>
  ) : null;
  return (
    <>
      <tr className={isOpen ? "is-open" : undefined}>
        <th scope="row">
          <div className="strong-500">{s.supplier}</div>
          <div className="sup__bar" aria-hidden="true">
            <div className="sup__bar-ok" style={{ width: share(s.byBucket.evidenced_cusma) }} />
            <div className="sup__bar-out" style={{ width: share(s.byBucket.stated_outside) }} />
            <div className="sup__bar-gap" style={{ width: share(s.byBucket.unproven) }} />
          </div>
          <div className="show-sm sup__sm">
            {asks}
            {button}
          </div>
        </th>
        <td className="num hide-sm">{s.lines.length}</td>
        <td className="num">{s.total.toFixed(2)}</td>
        <td className="num">{s.byBucket.evidenced_cusma ? s.byBucket.evidenced_cusma.toFixed(2) : "–"}</td>
        <td className="num hide-sm">{s.byBucket.stated_outside ? s.byBucket.stated_outside.toFixed(2) : "–"}</td>
        <td className={`num${s.byBucket.unproven ? " sup__gap" : ""}`}>
          {s.byBucket.unproven ? s.byBucket.unproven.toFixed(2) : "–"}
        </td>
        <td className="hide-sm">{asks}</td>
        <td className="sup__action hide-sm">{button}</td>
      </tr>
      {isOpen ? (
        <tr className="sup__draft-row" id={draftId}>
          <td colSpan={8}>
            <Draft s={s} productId={productId} />
          </td>
        </tr>
      ) : null}
    </>
  );
}
