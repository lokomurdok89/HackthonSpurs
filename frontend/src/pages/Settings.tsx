import { useState } from "react";
import { resetSettings, updateSettings, useActivityLog, useSettings } from "../data/settings";
import type { Passport } from "../data/types";
import { ActivityLog } from "./ActivityLog";
import { AppearanceSettings } from "./AppearanceSettings";
import "./Settings.css";

type Tab = "review" | "sharing" | "appearance" | "log";

const THRESHOLDS = [
  { value: 0, label: "Off" },
  { value: 0.9, label: "90% or higher" },
  { value: 0.95, label: "95% or higher" },
  { value: 0.98, label: "98% or higher" },
];

const DAYS = [
  { value: 30, label: "30 days" },
  { value: 90, label: "90 days" },
  { value: 365, label: "1 year" },
];

export function Row({ label, help, children }: { label: string; help: string; children: React.ReactNode }) {
  return (
    <div className="st__row">
      <div className="st__row-text">
        <div className="strong">{label}</div>
        <p className="muted small">{help}</p>
      </div>
      <div className="st__row-control">{children}</div>
    </div>
  );
}

export function Settings({ passport }: { passport: Passport }) {
  const s = useSettings();
  const log = useActivityLog();
  const [tab, setTab] = useState<Tab>("review");

  const shareable = passport.fields.filter((f) => f.status === "verified" || f.status === "missing");
  const sharedNow = s.defaultShared ?? shareable.map((f) => f.label);
  const toggleField = (label: string, on: boolean) => {
    const next = on ? [...sharedNow, label] : sharedNow.filter((l) => l !== label);
    updateSettings({ defaultShared: next }, `${on ? "Shared" : "Stopped sharing"} "${label}" by default`);
  };

  const tabs: { id: Tab; label: string; count?: number }[] = [
    { id: "review", label: "Review rules" },
    { id: "sharing", label: "Sharing defaults" },
    { id: "appearance", label: "Appearance" },
    { id: "log", label: "Activity log", count: log.length },
  ];

  return (
    <div className="st">
      <div className="st__head">
        <h1>Settings</h1>
        <p className="muted">Changes save as you make them and are recorded in the activity log.</p>
      </div>

      <div className="st__tabs" role="tablist" aria-label="Settings sections">
        {tabs.map((t) => (
          <button
            key={t.id}
            type="button"
            role="tab"
            id={`st-tab-${t.id}`}
            aria-selected={tab === t.id}
            aria-controls={`st-panel-${t.id}`}
            className="st__tab"
            onClick={() => setTab(t.id)}
          >
            {t.label}
            {t.count ? <span className="st__count">{t.count}</span> : null}
          </button>
        ))}
      </div>

      <section className="panel panel--flush" role="tabpanel" id={`st-panel-${tab}`} aria-labelledby={`st-tab-${tab}`}>
        {tab === "review" ? (
          <>
            <Row
              label="One-click approve"
              help="The review queue offers to approve every waiting value the reader was at least this sure of."
            >
              <fieldset className="st__radios">
                <legend className="visually-hidden">One-click approve threshold</legend>
                {THRESHOLDS.map((t) => (
                  <label key={t.value}>
                    <input
                      type="radio"
                      name="threshold"
                      checked={s.autoApproveAt === t.value}
                      onChange={() => updateSettings({ autoApproveAt: t.value }, `One-click approve set to ${t.label.toLowerCase()}`)}
                    />
                    {t.label}
                  </label>
                ))}
              </fieldset>
            </Row>
            <Row
              label="Second reviewer for corrections"
              help="A corrected value stays open until a second person checks it, and does not count as reviewed."
            >
              <label className="st__check">
                <input
                  type="checkbox"
                  checked={s.secondReviewer}
                  onChange={(e) =>
                    updateSettings({ secondReviewer: e.target.checked }, `Second reviewer for corrections turned ${e.target.checked ? "on" : "off"}`)
                  }
                />
                Require a second reviewer
              </label>
            </Row>
          </>
        ) : tab === "appearance" ? (
          <AppearanceSettings Row={Row} />
        ) : tab === "sharing" ? (
          <>
            <Row
              label="Fields shared by default"
              help="Switched on when you open a passport. Fields that need correction or are still waiting for review are never shared."
            >
              <div className="st__checks">
                {shareable.map((f) => (
                  <label key={f.label} className="st__check">
                    <input type="checkbox" checked={sharedNow.includes(f.label)} onChange={(e) => toggleField(f.label, e.target.checked)} />
                    {f.label}
                  </label>
                ))}
              </div>
            </Row>
            <Row label="Lot allocation" help="Show buyers which heats and lots went into the shipment.">
              <label className="st__check">
                <input
                  type="checkbox"
                  checked={s.defaultShowLots}
                  onChange={(e) => updateSettings({ defaultShowLots: e.target.checked }, `Lot allocation ${e.target.checked ? "shared" : "hidden"} by default`)}
                />
                Share lot allocation
              </label>
            </Row>
            <Row label="Buyer access length" help="How long a new buyer link works before it expires.">
              <select
                className="st__select"
                aria-label="Buyer access length"
                value={s.defaultAccessDays}
                onChange={(e) => {
                  const d = DAYS.find((x) => x.value === Number(e.target.value));
                  updateSettings({ defaultAccessDays: Number(e.target.value) }, `Buyer access length set to ${d?.label ?? e.target.value}`);
                }}
              >
                {DAYS.map((d) => (
                  <option key={d.value} value={d.value}>{d.label}</option>
                ))}
              </select>
            </Row>
          </>
        ) : (
          <ActivityLog entries={log} />
        )}
      </section>

      {tab !== "log" ? (
        <div>
          <button type="button" className="btn btn--ghost" onClick={resetSettings}>Restore defaults</button>
        </div>
      ) : null}
    </div>
  );
}
