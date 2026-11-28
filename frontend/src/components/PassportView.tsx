import type { Passport, PassportField } from "../data/types";
import { LogoIcon } from "./icons";
import "./PassportView.css";

const STATUS_TEXT: Record<PassportField["status"], string> = {
  verified: "Verified by manufacturer",
  pending: "Awaiting review",
  missing: "Not available",
  flagged: "Under correction",
};

interface PassportViewProps {
  passport: Passport;
  /** Labels of fields the manufacturer chose to share. Undefined shows every field. */
  shared?: ReadonlySet<string>;
  showLots?: boolean;
}

/** What a buyer sees behind the QR code. Unknown values are shown as unknown, never as zero. */
export function PassportView({ passport, shared, showLots = true }: PassportViewProps) {
  const fields = passport.fields.filter((f) => !shared || shared.has(f.label));

  return (
    <article className="pp theme-light" lang="en">
      <header className="pp__head">
        <div className="pp__brand">
          <LogoIcon size={16} />
          <span>{passport.manufacturer}</span>
        </div>
        <h1 className="pp__name">{passport.name}</h1>
        <div className="pp__id">
          Digital Product Passport for <span className="mono">{passport.productId}</span>
        </div>
      </header>

      <section className="pp__section" aria-labelledby="pp-fields">
        <h2 id="pp-fields">Product data</h2>
        {fields.length === 0 ? (
          <p className="pp__empty">The manufacturer has not shared any fields yet.</p>
        ) : (
          <dl className="pp__fields">
            {fields.map((f) => (
              <div key={f.label} className="pp__field">
                <dt>{f.label}</dt>
                <dd>
                  <span className={f.status === "missing" ? "pp__value pp__value--missing" : "pp__value"}>
                    {f.value}
                  </span>
                  <span className={`stamp pp__status pp__status--${f.status}`}>{STATUS_TEXT[f.status]}</span>
                  {f.source ? (
                    <span className="pp__source">
                      Source: {f.source.file}, {f.source.where}
                    </span>
                  ) : null}
                </dd>
              </div>
            ))}
          </dl>
        )}
      </section>

      {showLots ? (
        <section className="pp__section" aria-labelledby="pp-lots">
          <h2 id="pp-lots">Aluminium lots</h2>
          <p className="pp__note">{passport.allocationNote}</p>
          <table className="pp__lots">
            <thead>
              <tr>
                <th scope="col">Lot</th>
                <th scope="col" className="num">
                  Recycled
                </th>
                <th scope="col" className="num">
                  kg CO₂e/kg
                </th>
                <th scope="col">Cast in</th>
              </tr>
            </thead>
            <tbody>
              {passport.lots.map((l) => (
                <tr key={l.lot}>
                  <th scope="row">
                    <span className="pp__lot-id mono">{l.lot}</span>
                    <span className="pp__lot-form">{l.form}</span>
                    <span className="pp__source">
                      {l.source.file}, {l.source.where}
                    </span>
                  </th>
                  <td className="num">
                    {l.recycledPct === undefined ? <span className="pp__unknown">Not reported</span> : `${l.recycledPct}%`}
                  </td>
                  <td className="num">
                    {l.co2PerKg === undefined ? <span className="pp__unknown">Not reported</span> : l.co2PerKg.toFixed(1)}
                  </td>
                  <td>{l.castingCountry ?? <span className="pp__unknown">Not reported</span>}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </section>
      ) : null}

      <footer className="pp__foot">
        Data is held by the manufacturer and shared field by field. Every value names the document it came from.
      </footer>
    </article>
  );
}
