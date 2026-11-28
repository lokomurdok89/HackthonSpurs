import type { Finding, ProductEvidence as Product, SourceRef } from "../data/types";
import { EVIDENCE, cad, pct, summarize } from "../data/evidence";
import "./ProductEvidence.css";

function SourceLink({ source }: { source: SourceRef }) {
  // Opens the review view for now; will deep-link to the original page or cell once the API serves it.
  return (
    <a className="source-link" href="#/review" title={`Open ${source.file} at ${source.where}`}>
      {source.file} · {source.where}
    </a>
  );
}

function FindingCard({ f }: { f: Finding }) {
  return (
    <article className="finding">
      <div className="finding__meta">
        <span className={`tag tag--${f.severity}`}>{f.topic}</span>
        <span className="mono muted">{f.code}</span>
      </div>
      <h3 className="finding__title">{f.title}</h3>
      <p className="finding__body">{f.body}</p>
      <div className="finding__sources">
        {f.sources.map((s) => (
          <SourceLink key={`${s.file}-${s.where}`} source={s} />
        ))}
      </div>
      <p className="finding__next">
        <strong>Next step:</strong> {f.nextStep}
      </p>
    </article>
  );
}

export function ProductEvidence({ product }: { product: Product }) {
  const { total, byBucket, mix, largestUnknown } = summarize(product.bom);
  const highCount = product.findings.filter((f) => f.severity === "high").length;
  const bom = [...product.bom].sort((a, b) => b.costCad - a.costCad);
  const barLabel = mix.map((m) => `${EVIDENCE[m.type].label} ${pct(m.share)}`).join(", ");

  return (
    <div className="pe">
      <div className="pe__head">
        <div className="pe__title">
          <div className="crumbs">
            <a href="#/products">Products</a>
            <span aria-hidden="true">/</span>
            <span>{product.customer}</span>
          </div>
          <h1>
            {product.productId} · {product.description}
          </h1>
          <div className="pe__facts">
            <span>
              Customer part <span className="mono strong">{product.customerPart}</span>
            </span>
            <span>
              Drawing <span className="mono strong">{product.drawing}</span>
            </span>
            <span>{product.bom.length} BOM lines</span>
            <span>
              Standard material cost <strong>{cad(total)}</strong> / unit
            </span>
          </div>
        </div>
        <div className="pe__actions">
          <button type="button" className="btn">
            Draft supplier requests
          </button>
          <button type="button" className="btn btn--primary">
            Prepare certification draft
          </button>
        </div>
      </div>

      <p className="notice">
        This view shows the <strong>evidence behind each material cost</strong>. It is an estimate for a person to
        review, not a CUSMA regional value content result or a customs determination.
      </p>

      <section className="cards" aria-label="Evidence summary">
        <div className="card">
          <div className="card__label">Origin evidenced in CUSMA region</div>
          <div className="card__value">
            {cad(byBucket.evidenced_cusma)} <span>{pct(byBucket.evidenced_cusma / total)}</span>
          </div>
          <div className="card__sub">Mill test reports and supplier certifications for CA, US or MX</div>
        </div>
        <div className="card">
          <div className="card__label">Stated origin outside CUSMA</div>
          <div className="card__value">
            {cad(byBucket.stated_outside)} <span>{pct(byBucket.stated_outside / total)}</span>
          </div>
          <div className="card__sub">Country named on an invoice or certificate</div>
        </div>
        <div className="card">
          <div className="card__label">Unknown or not yet proven</div>
          <div className="card__value card__value--warn">
            {cad(byBucket.unproven)} <span>{pct(byBucket.unproven / total)}</span>
          </div>
          <div className="card__sub">
            {largestUnknown
              ? `Largest: ${largestUnknown.componentId}, ${cad(largestUnknown.costCad)}`
              : "Nothing unproven"}
          </div>
        </div>
        <div className="card">
          <div className="card__label">Open findings</div>
          <div className="card__value">
            {product.findings.length} <span>need a person</span>
          </div>
          <div className="card__sub">{highCount} high priority</div>
        </div>
      </section>

      <section className="panel" aria-labelledby="mix-title">
        <div className="panel__head">
          <h2 id="mix-title">Standard material cost per finished unit, by evidence type</h2>
          <span className="muted small">{cad(total)} total</span>
        </div>
        <div className="mixbar" role="img" aria-label={`Stacked bar: ${barLabel}`}>
          {mix.map((m) => (
            <div key={m.type} style={{ flexGrow: m.amount, background: EVIDENCE[m.type].color }} />
          ))}
        </div>
        <ul className="legend">
          {mix.map((m) => (
            <li key={m.type}>
              <span className="swatch" style={{ background: EVIDENCE[m.type].color }} aria-hidden="true" />
              <span>{EVIDENCE[m.type].label}</span>
              <span className="mono muted">
                {cad(m.amount)} · {pct(m.share)}
              </span>
            </li>
          ))}
        </ul>
      </section>

      <div className="pe__split">
        <section className="panel panel--flush pe__bom" aria-labelledby="bom-title">
          <div className="panel__head panel__head--pad">
            <h2 id="bom-title">Bill of materials and origin evidence</h2>
            <span className="muted small">Each source opens the original page or cell</span>
          </div>
          <div className="table-scroll">
            <table className="bom">
              <thead>
                <tr>
                  <th scope="col">Component</th>
                  <th scope="col">Supplier</th>
                  <th scope="col">BOM says</th>
                  <th scope="col">Evidence</th>
                  <th scope="col" className="num">
                    CAD / unit
                  </th>
                  <th scope="col">Source</th>
                </tr>
              </thead>
              <tbody>
                {bom.map((l) => {
                  const meta = EVIDENCE[l.evidence];
                  return (
                    <tr key={l.componentId}>
                      <td>
                        <div className="strong-500">{l.description}</div>
                        <div className="mono muted small">
                          {l.componentId} · HS {l.suggestedHs} <span className="suggested">suggested</span>
                        </div>
                      </td>
                      <td>{l.supplier}</td>
                      <td className="muted">{l.originPerBom}</td>
                      <td>
                        <span className="chip" style={{ background: meta.chipBg, color: meta.chipFg }}>
                          {meta.label}
                          {l.evidencedCountry ? `: ${l.evidencedCountry}` : ""}
                        </span>
                        <div className="note">{l.note}</div>
                      </td>
                      <td className="num mono">{l.costCad.toFixed(2)}</td>
                      <td>
                        <SourceLink source={l.source} />
                      </td>
                    </tr>
                  );
                })}
              </tbody>
              <tfoot>
                <tr>
                  <th scope="row" colSpan={4}>
                    Total standard material cost
                  </th>
                  <td className="num mono strong">{total.toFixed(2)}</td>
                  <td />
                </tr>
              </tfoot>
            </table>
          </div>
        </section>

        <aside className="pe__findings" aria-labelledby="find-title">
          <h2 id="find-title">Findings for a person</h2>
          {product.findings.map((f) => (
            <FindingCard key={f.code} f={f} />
          ))}
        </aside>
      </div>
    </div>
  );
}
