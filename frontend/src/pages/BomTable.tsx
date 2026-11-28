import type { BomLine, SourceRef } from "../data/types";
import { Cite, Stamp } from "./Cite";

interface BomTableProps {
  bom: BomLine[];
  total: number;
  numberOf: (s: SourceRef) => number;
  onOpenSource: (n: number) => void;
}

/** Bill of materials, largest cost first. Stacks into one entry per line on phones. */
export function BomTable({ bom, total, numberOf, onOpenSource }: BomTableProps) {
  return (
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
                  <Cite n={numberOf(l.source)} source={l.source} onOpen={onOpenSource} />
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
  );
}

/** Every cited document, numbered in reading order. */
export function SourcesList({ sources }: { sources: SourceRef[] }) {
  return (
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
  );
}
