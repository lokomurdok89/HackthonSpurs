import type { Finding, SourceRef } from "../data/types";

export function SourceLink({ source }: { source: SourceRef }) {
  // Opens the review view for now; will deep-link to the original page or cell once the API serves it.
  return (
    <a className="source-link" href="#/review" title={`Open ${source.file} at ${source.where}`}>
      {source.file} · {source.where}
    </a>
  );
}

export function FindingCard({ f }: { f: Finding }) {
  return (
    <article className="finding">
      <div className="finding__meta">
        <span className={`tag tag--${f.severity}`}>{f.topic}</span>
        <span className="mono muted">{f.code}</span>
      </div>
      <h3 className="finding__title">{f.title}</h3>
      <p className="finding__body">{f.body}</p>
      <div className="finding__sources">
        {f.sources.map((s) => (
          <SourceLink key={`${s.file}-${s.where}`} source={s} />
        ))}
      </div>
      <p className="finding__next">
        <strong>Next step:</strong> {f.nextStep}
      </p>
    </article>
  );
}
