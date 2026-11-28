import { useMemo, useState } from "react";
import { logActivity, useSettings } from "../data/settings";
import { QRCodeSVG } from "qrcode.react";
import type { Passport, PassportField } from "../data/types";
import { PassportView } from "../components/PassportView";
import "./Passports.css";

/** Only values a person has approved (or honest "not available" gaps) can leave the vault. */
const shareable = (f: PassportField) => f.status === "verified" || f.status === "missing";

const BLOCK_REASON: Partial<Record<PassportField["status"], string>> = {
  pending: "Approve in the Review queue first.",
  flagged: "Sources disagree.",
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
  const { defaultShared, defaultShowLots } = useSettings();
  const [shared, setShared] = useState<Set<string>>(
    () =>
      new Set(
        passport.fields
          .filter((f) => shareable(f) && (defaultShared === null || defaultShared.includes(f.label)))
          .map((f) => f.label),
      ),
  );
  const [showLots, setShowLots] = useState(defaultShowLots);
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
      logActivity("Copied passport link", `${passport.productId}, ${sharedIdx.length} fields shared`);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch {
      setCopied(false);
    }
  };

  return (
    <div className="ps">
      <div className="ps__head">
        <p className="ps__kind">Passport</p>
        <h1>
          <span className="ps__id">{passport.productId}</span> {passport.name}
        </h1>
        <p className="ps__summary" aria-label="Passport readiness">
          <span className="stamp pp__status pp__status--verified">{counts.verified} verified</span>
          {counts.flagged ? (
            <span className="stamp pp__status pp__status--flagged">{counts.flagged} need correction</span>
          ) : null}
          {counts.pending ? (
            <span className="stamp pp__status pp__status--pending">{counts.pending} awaiting review</span>
          ) : null}
          {counts.missing ? (
            <span className="stamp pp__status pp__status--missing">{counts.missing} not available</span>
          ) : null}
        </p>
      </div>

      <div className="ps__grid">
        <div className="ps__controls">
          <section className="panel panel--flush ps__section" aria-labelledby="share-title">
            <div className="ps__section-head">
              <h2 id="share-title">Fields to share</h2>
              <span className="muted small">
                {shared.size} of {passport.fields.length} shared. Only fields you switch on are shared.
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
                        <span className={`stamp pp__status pp__status--${f.status}`}>{STATUS_TEXT[f.status]}</span>
                        {f.source ? (
                          <a href="#/review" className="ps__source">
                            {f.source.file}, {f.source.where}
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

        <section className="panel panel--flush ps__preview" aria-labelledby="preview-label">
          <h2 className="ps__preview-label" id="preview-label">
            Buyer preview
          </h2>
          <div className="ps__preview-body">
            <PassportView passport={passport} shared={shared} showLots={showLots} />
          </div>
        </section>
      </div>
    </div>
  );
}
