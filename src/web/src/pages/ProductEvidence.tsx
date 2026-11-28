import { useState } from "react";
import type { BomLine, EvidenceType, Finding, ProductEvidence as Product, SourceRef } from "../data/types";
import { EVIDENCE, bucketOf, cad, pct, summarize, type OriginBucket } from "../data/evidence";
import { GapPlanner } from "./GapPlanner";
import { SupplierView } from "./SupplierView";
import "./ProductEvidence.css";

const BUCKETS: { id: OriginBucket; label: string; tone: string }[] = [
  { id: "evidenced_cusma", label: "Proven in the CUSMA region", tone: "ok" },
  { id: "stated_outside", label: "Stated as outside CUSMA", tone: "plain" },
  { id: "unproven", label: "Not proven", tone: "warn" },
];

/** Numbers every source document once, in reading order, so the table and findings cite the same list. */
function numberSources(bom: BomLine[], findings: Finding[]) {
  const list: SourceRef[] = [];
  const index = new Map<string, number>();
  const key = (s: SourceRef) => `${s.file}|${s.where}`;
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

type TabId = "plan" | "suppliers" | "bom" | "sources";

/** Shows a tab, then scrolls to and focuses an element inside it once it has rendered. */
function focusLater(id: string, block: ScrollLogicalPosition) {
  window.requestAnimationFrame(() => {
    const el = document.getElementById(id);
    if (!el) return;
    el.scrollIntoView({ block, behavior: "smooth" });
    el.focus({ preventScroll: true });
  });
}

function Cite({ n, source, onOpen }: { n: number; source: SourceRef; onOpen: (n: number) => void }) {
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

function Stamp({ type, country }: { type: EvidenceType; country?: string }) {
  const meta = EVIDENCE[type];
  return (
    <span className="stamp" style={{ color: meta.chipFg }}>
      {meta.label}
      {country ? ` (${country})` : ""}
    </span>
  );
}

export function ProductEvidence({ product }: { product: Product }) {
  const { total, byBucket, largestUnknown } = summarize(product.bom);
  const highCount = product.findings.filter((f) => f.severity === "high").length;
  const bom = [...product.bom].sort((a, b) => b.costCad - a.costCad);
  const { list: sources, numberOf } = numberSources(bom, product.findings);

  // Cost per evidence type inside each bucket, for the ledger and the bar.
  const rows = BUCKETS.map((b) => {
    const byType = new Map<EvidenceType, number>();
    for (const l of product.bom) {
      if (bucketOf(l) === b.id) byType.set(l.evidence, (byType.get(l.evidence) ?? 0) + l.costCad);
    }
    const parts = [...byType.entries()].map(([type, amount]) => ({ type, amount })).sort((x, y) => y.amount - x.amount);
    return { ...b, amount: byBucket[b.id], parts };
  });
  const barLabel = rows.map((r) => `${r.label} ${pct(r.amount / total)}`).join(", ");

  const [tab, setTab] = useState<TabId>("plan");
  const goToSource = (n: number) => {
    setTab("sources");
    focusLater(`src-${n}`, "center");
  };
  const goToSuppliers = () => {
    setTab("suppliers");
    focusLater("tabs", "start");
  };
  const tabs: { id: TabId; label: string; count?: number }[] = [
    { id: "plan", label: "Substantiation plan" },
    { id: "suppliers", label: "Supplier declarations" },
    { id: "bom", label: "Bill of materials", count: product.bom.length },
    { id: "sources", label: "Sources", count: sources.length },
  ];

  return (
    <div className="pe">
      <header className="pe__head">
        <div className="crumbs">
          <a href="#/products">Products</a>
          <span aria-hidden="true">/</span>
          <span>{product.productId}</span>
        </div>
        <div className="pe__titlerow">
          <div>
            <p className="pe__kind">Product</p>
            <h1>
              <span className="pe__id">{product.productId}</span> {product.description}
            </h1>
          </div>
          <div className="pe__actions">
            <button type="button" className="btn" onClick={goToSuppliers}>
              Draft supplier requests
            </button>
            <button type="button" className="btn btn--primary">
              Prepare certification draft
            </button>
          </div>
        </div>
      </header>

      <section className="panel recform" aria-label="Product details">
        <dl className="recform__grid">
          <div>
            <dt>Customer</dt>
            <dd>{product.customer}</dd>
          </div>
          <div>
            <dt>Customer part</dt>
            <dd className="mono">{product.customerPart}</dd>
          </div>
          <div>
            <dt>Drawing</dt>
            <dd className="mono">{product.drawing}</dd>
          </div>
          <div>
            <dt>BOM lines</dt>
            <dd>{product.bom.length}</dd>
          </div>
          <div>
            <dt>Standard material cost</dt>
            <dd>{cad(total)} / unit</dd>
          </div>
          <div>
            <dt>Open findings</dt>
            <dd>
              {product.findings.length} ({highCount} high)
            </dd>
          </div>
        </dl>
        <p className="pe__caveat">Not an RVC result or a customs determination.</p>
      </section>

      <div className="pe__top">
        <section className="panel ledger" aria-labelledby="ledger-title">
          <h2 id="ledger-title">Cost by evidence</h2>
          <div className="mixbar" role="img" aria-label={`Share of material cost: ${barLabel}`}>
            {rows.flatMap((r) =>
              r.parts.map((p) => (
                <div
                  key={`${r.id}-${p.type}`}
                  style={{ flexGrow: p.amount, background: EVIDENCE[p.type].color }}
                />
              )),
            )}
          </div>
          <table className="sum">
            <thead className="visually-hidden">
              <tr>
                <th scope="col">Evidence</th>
                <th scope="col">CAD per unit</th>
                <th scope="col">Share</th>
              </tr>
            </thead>
            {rows.map((r) => (
              <tbody key={r.id} className={`sum__group sum__group--${r.tone}`}>
                <tr className="sum__bucket">
                  <th scope="row">{r.label}</th>
                  <td className="num">{r.amount.toFixed(2)}</td>
                  <td className="num">{pct(r.amount / total)}</td>
                </tr>
                {r.parts.map((p) => (
                  <tr key={p.type} className="sum__part">
                    <th scope="row">
                      <span className="swatch" style={{ background: EVIDENCE[p.type].color }} aria-hidden="true" />
                      {EVIDENCE[p.type].label}
                    </th>
                    <td className="num">{p.amount.toFixed(2)}</td>
                    <td />
                  </tr>
                ))}
                {r.id === "unproven" && largestUnknown ? (
                  <tr className="sum__note">
                    <td colSpan={3}>
                      Largest: <span className="mono">{largestUnknown.componentId}</span>,{" "}
                      {cad(largestUnknown.costCad)}
                    </td>
                  </tr>
                ) : null}
              </tbody>
            ))}
            <tfoot>
              <tr>
                <th scope="row">Total</th>
                <td className="num">{total.toFixed(2)}</td>
                <td className="num">100%</td>
              </tr>
            </tfoot>
          </table>
        </section>

        <section className="panel findings" aria-labelledby="find-title">
          <h2 id="find-title">Findings</h2>
          <p className="findings__count">
            {product.findings.length} open, {highCount} high
          </p>
          <ol className="findings__list">
            {product.findings.map((f) => (
              <li key={f.code} className="finding">
                <div className="finding__meta">
                  <span className={`stamp finding__sev finding__sev--${f.severity}`}>
                    {f.severity === "high" ? "High" : "Medium"}
                  </span>
                  <span>{f.topic}</span>
                  <span className="mono">{f.code}</span>
                </div>
                <h3 className="finding__title">{f.title}</h3>
                <p className="finding__body">
                  {f.body}{" "}
                  <span className="cites">
                    {f.sources.map((s) => (
                      <Cite key={`${s.file}-${s.where}`} n={numberOf(s)} source={s} onOpen={goToSource} />
                    ))}
                  </span>
                </p>
                <p className="finding__next">
                  <span>Next step</span> {f.nextStep}
                </p>
              </li>
            ))}
          </ol>
        </section>
      </div>

      <div className="panel panel--flush rectabs">
        <div className="tabs" role="tablist" aria-label="Product records" id="tabs" tabIndex={-1}>
          {tabs.map((t) => (
            <button
              key={t.id}
              type="button"
              role="tab"
              id={`tab-${t.id}`}
              aria-selected={tab === t.id}
              aria-controls={`panel-${t.id}`}
              className="tab"
              onClick={() => setTab(t.id)}
            >
              {t.label}
              {t.count !== undefined ? <span className="tab__count">{t.count}</span> : null}
            </button>
          ))}
        </div>
        <div className="tabpanel" role="tabpanel" id={`panel-${tab}`} aria-labelledby={`tab-${tab}`}>
          {tab === "plan" ? (
              <GapPlanner bom={product.bom} total={total} proven={byBucket.evidenced_cusma} />
          ) : null}
          {tab === "suppliers" ? (
              <SupplierView bom={product.bom} productId={product.productId} />
          ) : null}
          {tab === "bom" ? (
              <section className="pe__bom" aria-labelledby="bom-title">
                <h2 id="bom-title" className="visually-hidden">Bill of materials</h2>
                <div className="table-scroll">
                  <table className="bom">
                    <thead>
                      <tr>
                        <th scope="col">Component</th>
                        <th scope="col">Supplier</th>
                        <th scope="col">BOM says</th>
                        <th scope="col">Evidence on file</th>
                        <th scope="col" className="num">
                          CAD / unit
                        </th>
                        <th scope="col" className="num">
                          Source
                        </th>
                      </tr>
                    </thead>
                    <tbody>
                      {bom.map((l) => (
                        <tr key={l.componentId}>
                          <td>
                            <div className="strong-500">{l.description}</div>
                            <div className="bom__codes">
                              <span className="mono">{l.componentId}</span>
                              <span>
                                HS <span className="mono">{l.suggestedHs}</span> <em>suggested</em>
                              </span>
                            </div>
                          </td>
                          <td data-label="Supplier">{l.supplier}</td>
                          <td
                            data-label="BOM says"
                            className={l.originPerBom.toLowerCase() === "unknown" ? "bom__unknown" : undefined}
                          >
                            {l.originPerBom}
                          </td>
                          <td>
                            <Stamp type={l.evidence} country={l.evidencedCountry} />
                            <div className="note">{l.note}</div>
                          </td>
                          <td className="num">{l.costCad.toFixed(2)}</td>
                          <td className="num">
                            <Cite n={numberOf(l.source)} source={l.source} onOpen={goToSource} />
                          </td>
                        </tr>
                      ))}
                    </tbody>
                    <tfoot>
                      <tr>
                        <th scope="row" colSpan={4}>
                          Total standard material cost
                        </th>
                        <td className="num">{total.toFixed(2)}</td>
                        <td />
                      </tr>
                    </tfoot>
                  </table>
                </div>
              </section>
          ) : null}
          {tab === "sources" ? (
              <section className="sources" aria-labelledby="src-title">
                <h2 id="src-title" className="visually-hidden">Sources</h2>
                <ol className="sources__list">
                  {sources.map((s, i) => (
                    <li key={`${s.file}-${s.where}`} id={`src-${i + 1}`} tabIndex={-1}>
                      <span className="sources__n">{i + 1}</span>
                      {/* Opens the review view for now; will deep-link to the original page or cell once the API serves it. */}
                      <a href="#/review" className="mono">
                        {s.file}
                      </a>
                      <span className="muted">{s.where}</span>
                    </li>
                  ))}
                </ol>
              </section>
          ) : null}
        </div>
      </div>
    </div>
  );
}
