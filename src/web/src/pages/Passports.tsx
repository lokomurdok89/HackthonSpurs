import { useMemo, useState } from "react";
import { QRCodeSVG } from "qrcode.react";
import type { Passport, PassportField } from "../data/types";
import { PassportView } from "../components/PassportView";
import "./Passports.css";

/** Only values a person has approved (or honest "not available" gaps) can leave the vault. */
const shareable = (f: PassportField) => f.status === "verified" || f.status === "missing";

const BLOCK_REASON: Partial<Record<PassportField["status"], string>> = {
  pending: "Approve this value in the Review queue before sharing it.",
  flagged: "Sources disagree. Resolve the conflict before sharing.",
};

const STATUS_TEXT: Record<PassportField["status"], string> = {
  verified: "Verified",
  pending: "Awaiting review",
  missing: "Not available",
  flagged: "Needs correction",
};

/** Builds the public link. Shared fields are encoded by index so the QR changes as you toggle. */
export function publicPassportUrl(productId: string, sharedIdx: number[], lots: boolean) {
  const q = new URLSearchParams({ share: sharedIdx.join(","), lots: lots ? "1" : "0" });
  return `${window.location.origin}${window.location.pathname}#/p/${encodeURIComponent(productId)}?${q}`;
}

export function Passports({ passport }: { passport: Passport }) {
  const [shared, setShared] = useState<Set<string>>(
    () => new Set(passport.fields.filter((f) => shareable(f)).map((f) => f.label)),
  );
  const [showLots, setShowLots] = useState(true);
  const [copied, setCopied] = useState(false);

  const counts = useMemo(() => {
    const by = (s: PassportField["status"]) => passport.fields.filter((f) => f.status === s).length;
    return { verified: by("verified"), flagged: by("flagged"), missing: by("missing"), pending: by("pending") };
  }, [passport]);

  const sharedIdx = passport.fields.flatMap((f, i) => (shared.has(f.label) ? [i] : []));
  const url = publicPassportUrl(passport.productId, sharedIdx, showLots);

  const toggle = (label: string) =>
    setShared((prev) => {
      const next = new Set(prev);
      if (next.has(label)) next.delete(label);
      else next.add(label);
      return next;
    });

  const copy = async () => {
    try {
      await navigator.clipboard.writeText(url);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch {
      setCopied(false);
    }
  };

  return (
    <div className="ps">
      <div className="ps__head">
        <div>
          <div className="muted small">Passports</div>
          <h1>
            {passport.productId} · {passport.name}
          </h1>
          <p className="muted">
            Choose what the buyer sees behind the QR code. Nothing leaves the vault unless you switch it on.
          </p>
        </div>
      </div>

      <section className="ps__ready" aria-label="Passport readiness">
        <div className="ready ready--ok">
          <strong>{counts.verified}</strong> verified
        </div>
        {counts.flagged ? (
          <div className="ready ready--bad">
            <strong>{counts.flagged}</strong> need correction
          </div>
        ) : null}
        {counts.missing ? (
          <div className="ready ready--none">
            <strong>{counts.missing}</strong> not available
          </div>
        ) : null}
        {counts.pending ? (
          <div className="ready ready--wait">
            <strong>{counts.pending}</strong> awaiting review
          </div>
        ) : null}
      </section>

      <div className="ps__grid">
        <div className="ps__controls">
          <section className="panel" aria-labelledby="share-title">
            <div className="panel__head">
              <h2 id="share-title">Fields to share</h2>
              <span className="muted small">
                {shared.size} of {passport.fields.length} shared
              </span>
            </div>
            <ul className="share-list">
              {passport.fields.map((f) => {
                const id = `share-${f.label.replace(/\W+/g, "-")}`;
                const can = shareable(f);
                return (
                  <li key={f.label} className={`share${can ? "" : " share--blocked"}`}>
                    <div className="share__text">
                      <label htmlFor={id} className="share__label">
                        {f.label}
                      </label>
                      <div className="share__value">{f.value}</div>
                      <div className="share__meta">
                        <span className={`pp__status pp__status--${f.status}`}>{STATUS_TEXT[f.status]}</span>
                        {f.source ? (
                          <a href="#/review" className="source-link">
                            {f.source.file} · {f.source.where}
                          </a>
                        ) : null}
                      </div>
                      {f.note ? <div className="share__note">{f.note}</div> : null}
                      {!can ? <div className="share__note share__note--block">{BLOCK_REASON[f.status]}</div> : null}
                    </div>
                    <input
                      id={id}
                      type="checkbox"
                      role="switch"
                      className="switch"
                      checked={shared.has(f.label)}
                      disabled={!can}
                      onChange={() => toggle(f.label)}
                    />
                  </li>
                );
              })}
              <li className="share">
                <div className="share__text">
                  <label htmlFor="share-lots" className="share__label">
                    Aluminium lot data
                  </label>
                  <div className="share__value">
                    {passport.lots.length} candidate lots: recycled content, CO₂ and casting country
                  </div>
                  <div className="share__note">{passport.allocationNote}</div>
                </div>
                <input
                  id="share-lots"
                  type="checkbox"
                  role="switch"
                  className="switch"
                  checked={showLots}
                  onChange={() => setShowLots((v) => !v)}
                />
              </li>
            </ul>
          </section>

          <section className="panel qr-card" aria-labelledby="qr-title">
            <div className="qr-card__code">
              <QRCodeSVG value={url} size={148} marginSize={2} title={`QR code for the ${passport.productId} passport`} />
            </div>
            <div className="qr-card__text">
              <h2 id="qr-title">Buyer link</h2>
              <p className="muted small">
                The code updates as you change what is shared. Scanning it opens the passport on the right.
              </p>
              <div className="qr-card__actions">
                <button type="button" className="btn btn--sm" onClick={copy}>
                  {copied ? "Copied" : "Copy link"}
                </button>
                <a className="btn btn--sm btn--primary qr-card__open" href={url} target="_blank" rel="noreferrer">
                  Open as buyer
                </a>
              </div>
              <span className="visually-hidden" aria-live="polite">
                {copied ? "Link copied" : ""}
              </span>
            </div>
          </section>
        </div>

        <section className="ps__preview" aria-label="Buyer preview">
          <div className="muted small ps__preview-label">What the buyer sees</div>
          <div className="phone">
            <div className="phone__screen">
              <PassportView passport={passport} shared={shared} showLots={showLots} />
            </div>
          </div>
        </section>
      </div>
    </div>
  );
}
