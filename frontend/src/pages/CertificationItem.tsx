import { parsePeriod, problemWith, SINGLE_SHIPMENT, statusOf, type CertContext, type Entry, type ItemSpec, type ItemStatus } from "../data/certification";

const STATUS: Record<ItemStatus, { label: string; tone: string }> = {
  ready: { label: "Filled", tone: "verified" },
  review: { label: "Needs review", tone: "pending" },
  missing: { label: "Missing", tone: "missing" },
};

interface Props {
  item: ItemSpec;
  entry: Entry;
  ctx: CertContext;
  /** Where a prefilled value came from. */
  source?: string;
  canEdit: boolean;
  onChange: (entry: Entry) => void;
}

/** One certification data element: its status, its input and the confirm action. */
export function CertificationItem({ item, entry, ctx, source, canEdit, onChange }: Props) {
  const status = statusOf(item.key, entry, ctx);
  const problem = entry.value ? problemWith(item.key, entry.value, ctx) : "";
  const id = `cert-${item.key}`;
  const period = item.key === "period" ? parsePeriod(entry.value) : undefined;
  const detail = item.options?.find((o) => o.value === entry.value)?.detail;
  /** Choices count as confirmed once valid; typed text waits for the Confirm button. */
  const choose = (value: string) => onChange({ value, confirmed: !problemWith(item.key, value, ctx) });

  return (
    <li className="cert__item">
      <div className="cert__text">
        <label htmlFor={id} className="strong">{item.label}</label>
        <p className="muted small">{item.help}</p>
        <span className={`stamp pp__status pp__status--${STATUS[status].tone}`}>{STATUS[status].label}</span>
      </div>
      <div className="cert__control">
        {item.options ? (
          <select id={id} value={entry.value} disabled={!canEdit} onChange={(e) => choose(e.target.value)}>
            <option value="">Choose one</option>
            {item.options.map((o) => <option key={o.value}>{o.value}</option>)}
          </select>
        ) : period ? (
          <>
            <select id={id} value={period.kind === "none" ? "" : period.kind} disabled={!canEdit}
              onChange={(e) => choose(e.target.value === "blanket" ? " to " : e.target.value === "single" ? SINGLE_SHIPMENT : "")}>
              <option value="">Choose one</option>
              <option value="single">{SINGLE_SHIPMENT}</option>
              <option value="blanket">Blanket period</option>
            </select>
            {period.kind === "single" ? (
              <label className="cert__sub">Invoice number, if known
                <input value={period.invoice} disabled={!canEdit}
                  onChange={(e) => choose(e.target.value.trim() ? `${SINGLE_SHIPMENT}, invoice ${e.target.value}` : SINGLE_SHIPMENT)} />
              </label>
            ) : null}
            {period.kind === "blanket" ? (
              <div className="cert__dates">
                <label className="cert__sub">From<input type="date" value={period.from} disabled={!canEdit} onChange={(e) => choose(`${e.target.value} to ${period.to}`)} /></label>
                <label className="cert__sub">To<input type="date" value={period.to} disabled={!canEdit} onChange={(e) => choose(`${period.from} to ${e.target.value}`)} /></label>
              </div>
            ) : null}
          </>
        ) : (
          <textarea id={id} rows={2} value={entry.value} readOnly={!canEdit}
            onChange={(e) => onChange({ value: e.target.value, confirmed: false })} />
        )}
        {detail ? <p className="muted small">{detail}</p> : null}
        {source ? <p className="muted small">Prefilled from the {source}.</p> : null}
        {problem ? <p className="cert__problem small" role="alert">{problem}</p> : null}
        {canEdit && !item.options && !period && status === "review" ? (
          <button type="button" className="btn btn--sm btn--ok" disabled={!!problem}
            onClick={() => onChange({ ...entry, confirmed: true })}>
            Confirm
          </button>
        ) : null}
      </div>
    </li>
  );
}
