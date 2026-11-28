import type { Finding, SourceRef } from "../data/types";
import { Cite } from "./Cite";

interface FindingsPanelProps {
  findings: Finding[];
  numberOf: (s: SourceRef) => number;
  onOpenSource: (n: number) => void;
}

/** Open findings with their cited sources and the next step for each. */
export function FindingsPanel({ findings, numberOf, onOpenSource }: FindingsPanelProps) {
  const highCount = findings.filter((f) => f.severity === "high").length;
  return (
    <section className="panel findings" aria-labelledby="find-title">
      <h2 id="find-title">Findings</h2>
      <p className="findings__count">
        {findings.length} open, {highCount} high
      </p>
      <ol className="findings__list">
        {findings.map((f) => (
          <li key={f.code} className="finding">
            <div className="finding__meta">
              <span className={`stamp finding__sev finding__sev--${f.severity}`}>
                {f.severity === "high" ? "High" : "Medium"}
              </span>
              <span>{f.topic}</span>
              <span className="mono">{f.code}</span>
            </div>
            <h3 className="finding__title">{f.title}</h3>
            <p className="finding__body">
              {f.body}{" "}
              <span className="cites">
                {f.sources.map((s) => (
                  <Cite key={`${s.file}-${s.where}`} n={numberOf(s)} source={s} onOpen={onOpenSource} />
                ))}
              </span>
            </p>
            <p className="finding__next">
              <span>Next step</span> {f.nextStep}
            </p>
          </li>
        ))}
      </ol>
    </section>
  );
}
