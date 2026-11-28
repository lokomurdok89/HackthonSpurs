import { useState } from "react";
import type { ExtractedField, ReviewStatus } from "../data/types";

export interface Decision {
  status: ReviewStatus;
  /** Set when a person corrected the value. */
  value?: string;
}

const STATUS_LABEL: Record<ReviewStatus, string> = {
  pending: "Waiting",
  approved: "Approved",
  corrected: "Corrected",
  rejected: "Rejected",
};

/** Map the supplied technical confidence score to its UI band. */
function confidenceLevel(c: number): "high" | "mid" | "low" {
  if (c >= 0.95) return "high";
  if (c >= 0.9) return "mid";
  return "low";
}

/** Render a review field and its current decision; expose focus and decision actions and highlight if requested. */
export function FieldRow({
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
  onDecide?: (d: Decision) => void;
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

      {editing && onDecide ? (
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
        {!onDecide ? null : decision.status === "pending" ? (
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
