import type { BomLine, EvidenceType } from "../data/types";
import { EVIDENCE, bucketOf, cad, pct, summarize, type OriginBucket } from "../data/evidence";

const BUCKETS: { id: OriginBucket; label: string; tone: string }[] = [
  { id: "evidenced_cusma", label: "Proven in the CUSMA region", tone: "ok" },
  { id: "stated_outside", label: "Stated as outside CUSMA", tone: "plain" },
  { id: "unproven", label: "Not proven", tone: "warn" },
];

/** Render nominal cost shares using the supplied bill of materials. */
export function CostLedger({ bom }: { bom: BomLine[] }) {
  const { total, byBucket, largestUnknown } = summarize(bom);
  // Cost per evidence type inside each bucket, for the ledger and the bar.
  const rows = BUCKETS.map((b) => {
    const byType = new Map<EvidenceType, number>();
    for (const l of bom) {
      if (bucketOf(l) === b.id) byType.set(l.evidence, (byType.get(l.evidence) ?? 0) + l.costCad);
    }
    const parts = [...byType.entries()].map(([type, amount]) => ({ type, amount })).sort((x, y) => y.amount - x.amount);
    return { ...b, amount: byBucket[b.id], parts };
  });
  const barLabel = rows.map((r) => `${r.label} ${pct(r.amount / total)}`).join(", ");

  return (
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
  );
}
