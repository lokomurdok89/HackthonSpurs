import { useMemo, useState } from "react";
import { logActivity, useSettings } from "../data/settings";
import type { ReviewItem } from "../data/types";
import { FieldRow, type Decision } from "./ReviewField";
import "./ReviewQueue.css";

type Decisions = Record<string, Decision>;

const ACTION: Record<Decision["status"], string> = {
  pending: "Undid decision",
  approved: "Approved value",
  corrected: "Corrected value",
  rejected: "Rejected value",
};

/** Build a stable field key from its parent review item and field identifiers. */
const keyOf = (itemId: string, fieldId: string) => `${itemId}/${fieldId}`;

/** Wrap the supplied filename after common separators without changing the name. */
function FileName({ name }: { name: string }) {
  const parts = name.split(/(?<=[_.-])/);
  return (
    <>
      {parts.map((p, i) => (
        <span key={i}>
          {p}
          {i < parts.length - 1 ? <wbr /> : null}
        </span>
      ))}
    </>
  );
}

/** Render the provided demo review items and either allow or suppress local decisions according to the role. */
export function ReviewQueue({ items, canReview }: { items: ReviewItem[]; canReview: boolean }) {
  const [selectedId, setSelectedId] = useState(items[0]?.id);
  const [decisions, setDecisions] = useState<Decisions>({});
  const [hoverField, setHoverField] = useState<string | undefined>();
  const { autoApproveAt, secondReviewer } = useSettings();

  const item = items.find((i) => i.id === selectedId) ?? items[0];

  /** Retrieve the decision for the supplied item and field or mark it pending. */
  const decisionFor = (itemId: string, fieldId: string): Decision =>
    decisions[keyOf(itemId, fieldId)] ?? { status: "pending" };

  /** Count the fields still pending on the supplied review item. */
  const pendingIn = (it: ReviewItem) => it.fields.filter((f) => decisionFor(it.id, f.id).status === "pending").length;

  const totals = useMemo(() => {
    const all = items.flatMap((it) => it.fields.map((f) => decisions[keyOf(it.id, f.id)]?.status ?? "pending"));
    return { all: all.length, done: all.filter((s) => s !== "pending" && !(secondReviewer && s === "corrected")).length };
  }, [items, decisions, secondReviewer]);

  if (!item) {
    return <p className="muted">Nothing is waiting for review.</p>;
  }

  /** Apply the supplied decision to the selected item's indicated field, and log it. */
  const decide = (fieldId: string, d: Decision) => {
    const f = item.fields.find((x) => x.id === fieldId);
    logActivity(ACTION[d.status], `${f?.label ?? fieldId} in ${item.file}${d.value ? `, now "${d.value}"` : ""}`);
    setDecisions((prev) => ({ ...prev, [keyOf(item.id, fieldId)]: d }));
  };

  /** Approve high-confidence pending fields for the currently selected demo item. */
  const approveConfident = () => {
    logActivity("Bulk approved", `${confidentPending} values at ${pct}% or higher in ${item.file}`);
    setDecisions((prev) => {
      const next = { ...prev };
      for (const f of item.fields) {
        const k = keyOf(item.id, f.id);
        if ((next[k]?.status ?? "pending") === "pending" && f.confidence >= autoApproveAt) next[k] = { status: "approved" };
      }
      return next;
    });
  };

  const pct = Math.round(autoApproveAt * 100);
  const confidentPending = item.fields.filter(
    (f) => autoApproveAt > 0 && f.confidence >= autoApproveAt && decisionFor(item.id, f.id).status === "pending",
  ).length;

  return (
    <div className="rq">
      <div className="rq__head">
        <div>
          <h1>Review queue</h1>
          <p className="muted">
            {totals.done} of {totals.all} values reviewed
          </p>
        </div>
        <div
          className="progress"
          role="progressbar"
          aria-label="Values reviewed"
          aria-valuemin={0}
          aria-valuemax={totals.all}
          aria-valuenow={totals.done}
        >
          <div style={{ width: `${totals.all ? (totals.done / totals.all) * 100 : 0}%` }} />
        </div>
      </div>

      <div className="rq__grid">
        <nav className="rq__list" aria-label="Documents waiting for review">
          <h2 className="rq__colhead">Documents</h2>
          <ul>
            {items.map((it) => {
              const pending = pendingIn(it);
              return (
                <li key={it.id}>
                  <button
                    type="button"
                    className="doc-btn"
                    aria-current={it.id === item.id ? "true" : undefined}
                    onClick={() => {
                      setSelectedId(it.id);
                      setHoverField(undefined);
                    }}
                  >
                    <span className="doc-btn__name mono">
                      <FileName name={it.file} />
                    </span>
                    <span className="doc-btn__meta">
                      {it.docType}, {it.supplier}
                      {it.language === "fr" ? (
                        <span className="lang" title="French source">
                          FR
                        </span>
                      ) : null}
                    </span>
                    <span className={`doc-btn__count${pending ? "" : " doc-btn__count--done"}`}>
                      {pending ? `${pending} to review` : "Done"}
                    </span>
                  </button>
                </li>
              );
            })}
          </ul>
        </nav>

        <section className="rq__preview" aria-labelledby="preview-title">
          <div className="rq__preview-head">
            <h2 id="preview-title" className="rq__colhead mono preview-title">
              <FileName name={item.file} />
            </h2>
            <span className="muted small">
              Page 1, {item.method === "ocr" ? "scanned (OCR)" : "text layer"}
            </span>
          </div>
          <div className="paper" lang={item.language}>
            {item.previewLines.map((line, i) => (
              <p key={i} className={line.hit && line.hit === hoverField ? "paper__hit" : undefined}>
                {line.text}
              </p>
            ))}
          </div>
        </section>

        <section className="rq__fields" aria-labelledby="fields-title">
          <div className="rq__fields-head">
            <h2 id="fields-title" className="rq__colhead">
              Extracted values
            </h2>
            {canReview && autoApproveAt > 0 ? (
              <button type="button" className="btn btn--sm" disabled={!confidentPending} onClick={approveConfident}>
                Approve {confidentPending} at {pct}%+
              </button>
            ) : null}
          </div>
          <ul className="fields">
            {item.fields.map((f) => (
              <FieldRow
                key={`${item.id}/${f.id}`}
                field={f}
                decision={decisionFor(item.id, f.id)}
                highlighted={hoverField === f.id}
                onFocus={() => setHoverField(f.id)}
                onDecide={canReview ? (d) => decide(f.id, d) : undefined}
                needsSecond={secondReviewer}
              />
            ))}
          </ul>
        </section>
      </div>
    </div>
  );
}
