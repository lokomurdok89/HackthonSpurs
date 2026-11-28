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
    <article className="pp" lang="en">
      <header className="pp__head">
        <div className="pp__kicker">
          <LogoIcon size={16} />
          Digital Product Passport
        </div>
        <h1 className="pp__name">{passport.name}</h1>
        <div className="pp__id">{passport.productId}</div>
        <div className="pp__maker">{passport.manufacturer}</div>
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
                  <span className={`pp__status pp__status--${f.status}`}>{STATUS_TEXT[f.status]}</span>
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
          <div className="pp__lots">
            {passport.lots.map((l) => (
              <div key={l.lot} className="pp__lot">
                <div className="pp__lot-head">
                  <span className="pp__lot-id">Lot {l.lot}</span>
                  <span className="pp__lot-form">{l.form}</span>
                </div>
                <dl className="pp__lot-grid">
                  <div>
                    <dt>Recycled content</dt>
                    <dd>{l.recycledPct === undefined ? <span className="pp__unknown">Not reported</span> : `${l.recycledPct}%`}</dd>
                  </div>
                  <div>
                    <dt>kg CO₂e per kg Al</dt>
                    <dd>{l.co2PerKg === undefined ? <span className="pp__unknown">Not reported</span> : l.co2PerKg.toFixed(1)}</dd>
                  </div>
                  <div>
                    <dt>Cast in</dt>
                    <dd>{l.castingCountry ?? <span className="pp__unknown">Not reported</span>}</dd>
                  </div>
                </dl>
                <div className="pp__source">
                  Source: {l.source.file}, {l.source.where}
                </div>
              </div>
            ))}
          </div>
        </section>
      ) : null}

      <footer className="pp__foot">
        Data is held by the manufacturer and shared field by field. Every value names the document it came from.
      </footer>
    </article>
  );
}
