import { useMemo, useState } from "react";
import type { ExtractedField, ReviewItem, ReviewStatus } from "../data/types";
import "./ReviewQueue.css";

interface Decision {
  status: ReviewStatus;
  /** Set when a person corrected the value. */
  value?: string;
}

type Decisions = Record<string, Decision>;

const keyOf = (itemId: string, fieldId: string) => `${itemId}/${fieldId}`;

const STATUS_LABEL: Record<ReviewStatus, string> = {
  pending: "Waiting",
  approved: "Approved",
  corrected: "Corrected",
  rejected: "Rejected",
};

/** Lets long file names wrap after an underscore, hyphen or dot instead of mid-word. */
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

function confidenceLevel(c: number): "high" | "mid" | "low" {
  if (c >= 0.95) return "high";
  if (c >= 0.9) return "mid";
  return "low";
}

function FieldRow({
  field,
  decision,
  highlighted,
  onFocus,
  onDecide,
}: {
  field: ExtractedField;
  decision: Decision;
  highlighted: boolean;
  onFocus: () => void;
  onDecide: (d: Decision) => void;
}) {
  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState(decision.value ?? field.value);
  const shown = decision.value ?? field.value;
  const level = confidenceLevel(field.confidence);
  const inputId = `edit-${field.id}`;

  return (
    <li
      className={`field field--${decision.status}${highlighted ? " field--hl" : ""}`}
      onMouseEnter={onFocus}
      onFocus={onFocus}
    >
      <div className="field__top">
        <span className="field__label">{field.label}</span>
        <span className={`conf conf--${level}`} title="How sure the reader was">
          {Math.round(field.confidence * 100)}%
        </span>
      </div>

      {editing ? (
        <form
          className="field__edit"
          onSubmit={(e) => {
            e.preventDefault();
            const v = draft.trim();
            if (v && v !== field.value) onDecide({ status: "corrected", value: v });
            else onDecide({ status: "approved" });
            setEditing(false);
          }}
        >
          <label htmlFor={inputId} className="visually-hidden">
            Corrected value for {field.label}
          </label>
          <input id={inputId} value={draft} onChange={(e) => setDraft(e.target.value)} autoFocus />
          <button type="submit" className="btn btn--primary btn--sm">
            Save
          </button>
          <button type="button" className="btn btn--sm" onClick={() => setEditing(false)}>
            Cancel
          </button>
        </form>
      ) : (
        <div className="field__value">
          {decision.status === "corrected" ? <s className="muted">{field.value}</s> : null}
          <span>{shown}</span>
        </div>
      )}

      {field.quote ? (
        <div className="field__quote">
          <span className="mono">“{field.quote}”</span>
          {field.translation ? <span className="field__en muted">English: {field.translation}</span> : null}
        </div>
      ) : null}

      <div className="field__bottom">
        <span className="muted small">{field.where}</span>
        {decision.status === "pending" ? (
          <div className="field__actions">
            <button type="button" className="btn btn--sm btn--ok" onClick={() => onDecide({ status: "approved" })}>
              Approve
            </button>
            <button type="button" className="btn btn--sm" onClick={() => setEditing(true)}>
              Edit
            </button>
            <button type="button" className="btn btn--sm btn--ghost" onClick={() => onDecide({ status: "rejected" })}>
              Reject
            </button>
          </div>
        ) : (
          <div className="field__actions">
            <span className={`stamp status status--${decision.status}`}>{STATUS_LABEL[decision.status]}</span>
            <button type="button" className="btn btn--sm btn--ghost" onClick={() => onDecide({ status: "pending" })}>
              Undo
            </button>
          </div>
        )}
      </div>
    </li>
  );
}

export function ReviewQueue({ items }: { items: ReviewItem[] }) {
  const [selectedId, setSelectedId] = useState(items[0]?.id);
  const [decisions, setDecisions] = useState<Decisions>({});
  const [hoverField, setHoverField] = useState<string | undefined>();

  const item = items.find((i) => i.id === selectedId) ?? items[0];

  const decisionFor = (itemId: string, fieldId: string): Decision =>
    decisions[keyOf(itemId, fieldId)] ?? { status: "pending" };

  const pendingIn = (it: ReviewItem) => it.fields.filter((f) => decisionFor(it.id, f.id).status === "pending").length;

  const totals = useMemo(() => {
    const all = items.flatMap((it) => it.fields.map((f) => decisions[keyOf(it.id, f.id)]?.status ?? "pending"));
    return { all: all.length, done: all.filter((s) => s !== "pending").length };
  }, [items, decisions]);

  if (!item) {
    return <p className="muted">Nothing is waiting for review.</p>;
  }

  const decide = (fieldId: string, d: Decision) =>
    setDecisions((prev) => ({ ...prev, [keyOf(item.id, fieldId)]: d }));

  const approveConfident = () =>
    setDecisions((prev) => {
      const next = { ...prev };
      for (const f of item.fields) {
        const k = keyOf(item.id, f.id);
        if ((next[k]?.status ?? "pending") === "pending" && f.confidence >= 0.95) next[k] = { status: "approved" };
      }
      return next;
    });

  const confidentPending = item.fields.filter(
    (f) => f.confidence >= 0.95 && decisionFor(item.id, f.id).status === "pending",
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
            <button type="button" className="btn btn--sm" disabled={!confidentPending} onClick={approveConfident}>
              Approve {confidentPending} at 95%+
            </button>
          </div>
          <ul className="fields">
            {item.fields.map((f) => (
              <FieldRow
                key={`${item.id}/${f.id}`}
                field={f}
                decision={decisionFor(item.id, f.id)}
                highlighted={hoverField === f.id}
                onFocus={() => setHoverField(f.id)}
                onDecide={(d) => decide(f.id, d)}
              />
            ))}
          </ul>
        </section>
      </div>
    </div>
  );
}
