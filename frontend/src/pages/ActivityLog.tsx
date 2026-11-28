import { useState } from "react";
import type { LogEntry } from "../data/settings";

const when = (iso: string) =>
  new Date(iso).toLocaleString("en-CA", { month: "short", day: "numeric", hour: "numeric", minute: "2-digit" });

function toCsv(rows: LogEntry[]) {
  const esc = (v: string) => `"${v.replace(/"/g, '""')}"`;
  const lines = rows.map((r) => [r.at, r.who, r.action, r.detail].map(esc).join(","));
  return ["Time,Who,Action,Detail", ...lines].join("\n");
}

export function ActivityLog({ entries }: { entries: LogEntry[] }) {
  const [filter, setFilter] = useState("all");
  const actions = [...new Set(entries.map((e) => e.action))].sort();
  const shown = filter === "all" ? entries : entries.filter((e) => e.action === filter);

  const download = () => {
    const blob = new Blob([toCsv(shown)], { type: "text/csv" });
    const a = document.createElement("a");
    a.href = URL.createObjectURL(blob);
    a.download = "origin-vault-activity.csv";
    a.click();
    URL.revokeObjectURL(a.href);
  };

  if (!entries.length) {
    return (
      <div className="st__empty">
        <div className="strong">No activity yet</div>
        <p className="muted">Approve a value in the review queue, give a buyer access or change a setting, and it shows up here.</p>
      </div>
    );
  }

  return (
    <>
      <div className="st__bar">
        <select className="st__select" aria-label="Filter by action" value={filter} onChange={(e) => setFilter(e.target.value)}>
          <option value="all">All actions</option>
          {actions.map((a) => (
            <option key={a} value={a}>{a}</option>
          ))}
        </select>
        <button type="button" className="btn btn--sm" onClick={download}>Export CSV</button>
      </div>
      <table className="st__log">
        <thead>
          <tr>
            <th scope="col">Time</th>
            <th scope="col">Who</th>
            <th scope="col">Action</th>
            <th scope="col">Detail</th>
          </tr>
        </thead>
        <tbody>
          {shown.map((e, i) => (
            <tr key={`${e.at}-${i}`}>
              <td data-label="Time" className="st__time">{when(e.at)}</td>
              <td data-label="Who">{e.who}</td>
              <td data-label="Action" className="strong">{e.action}</td>
              <td data-label="Detail" className="st__detail">{e.detail}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </>
  );
}
