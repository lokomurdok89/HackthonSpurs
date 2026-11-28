import { useState } from "react";
import type { BomLine } from "../data/types";
import { bucketOf, cad, neededDocument, pct } from "../data/evidence";

interface GapPlannerProps {
  bom: BomLine[];
  total: number;
  proven: number;
}

/** Show an illustrative plan based on the supplied BOM, total cost and currently evidenced amount. */
export function GapPlanner({ bom, total, proven }: GapPlannerProps) {
  const gaps = bom
    .filter((l) => bucketOf(l) === "unproven")
    .sort((a, b) => b.costCad - a.costCad)
    .map((line) => ({ line, document: neededDocument(line)! }));
  const outside = bom.filter((l) => bucketOf(l) === "stated_outside").reduce((s, l) => s + l.costCad, 0);

  const [picked, setPicked] = useState<Set<string>>(() => new Set());
  /** Toggle selection of a gap for the indicated component ID. */
  const toggle = (id: string) =>
    setPicked((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });

  const added = gaps.filter((g) => picked.has(g.line.componentId)).reduce((s, g) => s + g.line.costCad, 0);
  const nowShare = proven / total;
  const planShare = (proven + added) / total;
  const allPicked = gaps.length > 0 && picked.size === gaps.length;

  return (
    <section className="plan" aria-labelledby="plan-title">
      <div className="plan__head">
        <div>
          <h2 id="plan-title" className="visually-hidden">Origin substantiation plan</h2>
          <p className="plan__intro">Assumes each document confirms CA, US or MX origin.</p>
        </div>
        <button
          type="button"
          className="btn btn--sm"
          onClick={() => setPicked(allPicked ? new Set() : new Set(gaps.map((g) => g.line.componentId)))}
          disabled={gaps.length === 0}
        >
          {allPicked ? "Clear all" : "Tick all"}
        </button>
      </div>

      <div className="plan__meter">
        <div className="plan__bar" aria-hidden="true">
          <div className="plan__bar-now" style={{ width: `${nowShare * 100}%` }} />
          <div className="plan__bar-add" style={{ width: `${(added / total) * 100}%` }} />
        </div>
        <p className="plan__readout" aria-live="polite">
          Proven share <strong>{pct(nowShare)}</strong>
          {picked.size > 0 ? (
            <>
              {" "}
              <span aria-hidden="true">→</span>
              <span className="visually-hidden">would become</span> <strong className="plan__to">{pct(planShare)}</strong>{" "}
              with {picked.size} {picked.size === 1 ? "document" : "documents"} ({cad(added)})
            </>
          ) : (
            null
          )}
        </p>
      </div>

      {gaps.length === 0 ? (
        <p className="plan__empty">No unproven lines.</p>
      ) : (
        <div className="table-scroll">
          <table className="plan__table">
            <thead>
              <tr>
                <th scope="col" className="plan__check">
                  <span className="visually-hidden">Plan to get</span>
                </th>
                <th scope="col">Line</th>
                <th scope="col" className="hide-sm">Supplier</th>
                <th scope="col" className="hide-sm">Document needed</th>
                <th scope="col" className="num">
                  CAD / unit
                </th>
                <th scope="col" className="num">
                  Adds
                </th>
              </tr>
            </thead>
            <tbody>
              {gaps.map(({ line, document }) => {
                const id = `plan-${line.componentId}`;
                const on = picked.has(line.componentId);
                return (
                  <tr key={line.componentId} className={on ? "is-picked" : undefined} onClick={(e) => { if (!(e.target as HTMLElement).closest("input, label")) toggle(line.componentId); }}>
                    <td className="plan__check">
                      <input
                        id={id}
                        type="checkbox"
                        checked={on}
                        onChange={() => toggle(line.componentId)}
                      />
                    </td>
                    <td>
                      <label htmlFor={id} className="strong-500">
                        {line.description}
                      </label>
                      <div className="mono muted small">{line.componentId}</div>
                      <div className="show-sm plan__sm">
                        <span className="muted">{line.supplier}</span>
                        <span>{document}</span>
                      </div>
                    </td>
                    <td className="hide-sm">{line.supplier}</td>
                    <td className="hide-sm">{document}</td>
                    <td className="num">{line.costCad.toFixed(2)}</td>
                    <td className="num plan__adds">+{((line.costCad / total) * 100).toFixed(1)} pts</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}

      {outside > 0 ? (
        <p className="plan__foot">
          Excludes lines stated outside CUSMA ({cad(outside)}).
        </p>
      ) : null}
    </section>
  );
}
