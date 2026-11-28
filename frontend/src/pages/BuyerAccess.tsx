import { useState, type FormEvent } from "react";
import { publicPassportUrl } from "../data/passportUrl";
import { logActivity, useSettings } from "../data/settings";
import type { Passport } from "../data/types";
import "./BuyerAccess.css";

type AccessState = "active" | "expired" | "revoked";

interface Grant {
  id: string;
  company: string;
  contact: string;
  expires: string;
  lastOpened: string | null;
  state: AccessState;
}

// Fictional buyers for the demo workspace.
const SEED: Grant[] = [
  { id: "g1", company: "Northgate Assembly", contact: "purchasing@northgate.example", expires: "2027-03-31", lastOpened: "2026-11-26", state: "active" },
  { id: "g2", company: "Laurier Fleet Parts", contact: "j.ouellet@laurierfleet.example", expires: "2027-01-15", lastOpened: null, state: "active" },
  { id: "g3", company: "Harbour Rail Systems", contact: "audit@harbourrail.example", expires: "2026-10-31", lastOpened: "2026-10-02", state: "expired" },
];

const STATE_LABEL: Record<AccessState, string> = { active: "Active", expired: "Expired", revoked: "Revoked" };
const STATE_CLASS: Record<AccessState, string> = { active: "verified", expired: "pending", revoked: "missing" };

const fmt = (iso: string) =>
  new Date(`${iso}T12:00:00`).toLocaleDateString("en-CA", { year: "numeric", month: "short", day: "numeric" });

function inDays(n: number) {
  const d = new Date();
  d.setDate(d.getDate() + n);
  return d.toISOString().slice(0, 10);
}

export function BuyerAccess({ passport }: { passport: Passport }) {
  const [grants, setGrants] = useState<Grant[]>(SEED);
  const [company, setCompany] = useState("");
  const [contact, setContact] = useState("");
  const { defaultAccessDays } = useSettings();
  const [days, setDays] = useState(String(defaultAccessDays));
  const [copied, setCopied] = useState<string | null>(null);

  const sharedIdx = passport.fields.flatMap((f, i) => (f.status === "verified" || f.status === "missing" ? [i] : []));
  const url = publicPassportUrl(passport.productId, sharedIdx, true);
  const active = grants.filter((g) => g.state === "active").length;

  const invite = (e: FormEvent) => {
    e.preventDefault();
    if (!company.trim() || !contact.trim()) return;
    const g: Grant = { id: `g${Date.now()}`, company: company.trim(), contact: contact.trim(), expires: inDays(Number(days)), lastOpened: null, state: "active" };
    logActivity("Gave buyer access", `${g.company} (${g.contact}) until ${g.expires}`);
    setGrants((prev) => [g, ...prev]);
    setCompany("");
    setContact("");
  };

  const revoke = (id: string) => {
    const g = grants.find((x) => x.id === id);
    if (g) logActivity("Revoked buyer access", g.company);
    setGrants((prev) => prev.map((g) => (g.id === id ? { ...g, state: "revoked" } : g)));
  };

  const copy = async (id: string) => {
    try {
      await navigator.clipboard.writeText(url);
      logActivity("Copied buyer link", grants.find((g) => g.id === id)?.company ?? id);
      setCopied(id);
      setTimeout(() => setCopied(null), 2000);
    } catch {
      setCopied(null);
    }
  };

  return (
    <div className="ba">
      <div className="ba__head">
        <p className="ba__kind">Buyer access</p>
        <h1>
          <span className="ba__id">{passport.productId}</span> {passport.name}
        </h1>
        <p className="muted">
          {active} {active === 1 ? "buyer has" : "buyers have"} access. Each buyer sees the {sharedIdx.length} fields set on the Passports screen.
        </p>
      </div>

      <section className="panel ba__invite" aria-labelledby="invite-title">
        <h2 id="invite-title">Give a buyer access</h2>
        <form className="ba__form" onSubmit={invite}>
          <label>
            <span>Company</span>
            <input value={company} onChange={(e) => setCompany(e.target.value)} required />
          </label>
          <label>
            <span>Contact email</span>
            <input type="email" value={contact} onChange={(e) => setContact(e.target.value)} required />
          </label>
          <label>
            <span>Access lasts</span>
            <select value={days} onChange={(e) => setDays(e.target.value)}>
              <option value="30">30 days</option>
              <option value="90">90 days</option>
              <option value="365">1 year</option>
            </select>
          </label>
          <button type="submit" className="btn btn--primary">Give access</button>
        </form>
      </section>

      <section className="panel panel--flush" aria-labelledby="grants-title">
        <div className="ba__bar">
          <h2 id="grants-title">Buyers with access</h2>
          <span className="muted small">{grants.length} total</span>
        </div>
        <table className="ba__table">
          <thead>
            <tr>
              <th scope="col">Buyer</th>
              <th scope="col">Status</th>
              <th scope="col">Expires</th>
              <th scope="col">Last opened</th>
              <th scope="col"><span className="visually-hidden">Actions</span></th>
            </tr>
          </thead>
          <tbody>
            {grants.map((g) => (
              <tr key={g.id} className={g.state === "active" ? "" : "ba__row--off"}>
                <td data-label="Buyer">
                  <div className="strong">{g.company}</div>
                  <div className="muted small ba__contact">{g.contact}</div>
                </td>
                <td data-label="Status">
                  <span className={`stamp pp__status pp__status--${STATE_CLASS[g.state]}`}>{STATE_LABEL[g.state]}</span>
                </td>
                <td data-label="Expires">{fmt(g.expires)}</td>
                <td data-label="Last opened">{g.lastOpened ? fmt(g.lastOpened) : <span className="muted">Not opened yet</span>}</td>
                <td className="ba__actions">
                  {g.state === "active" ? (
                    <>
                      <button type="button" className="btn btn--sm" onClick={() => copy(g.id)}>
                        {copied === g.id ? "Link copied" : "Copy link"}
                      </button>
                      <button type="button" className="btn btn--sm btn--ghost" onClick={() => revoke(g.id)}>
                        Revoke
                      </button>
                    </>
                  ) : null}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>
    </div>
  );
}
