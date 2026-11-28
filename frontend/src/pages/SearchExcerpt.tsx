import { useState, type ReactNode } from "react";

const COLLAPSE_CHARS = 380;

function escapeRegex(text: string): string {
  return text.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

function highlightText(text: string, terms: string[]): ReactNode {
  if (terms.length === 0) return text;
  const pattern = new RegExp(`(${terms.map(escapeRegex).join("|")})`, "gi");
  const parts = text.split(pattern);
  if (parts.length === 1) return text;
  return parts.map((part, i) =>
    i % 2 === 1 ? (
      <mark key={i} className="docs__mark">
        {part}
      </mark>
    ) : (
      part
    ),
  );
}

type Block =
  | { kind: "text"; lines: string[] }
  | { kind: "table"; rows: string[][] };

function parseBlocks(text: string): Block[] {
  const lines = text.split("\n").map((l) => l.trim()).filter(Boolean);
  const blocks: Block[] = [];

  for (const line of lines) {
    const cells = line.includes(" | ") ? line.split(" | ").map((c) => c.trim()) : null;
    if (cells && cells.length >= 2) {
      const prev = blocks[blocks.length - 1];
      if (prev && prev.kind === "table" && prev.rows[0].length === cells.length) {
        prev.rows.push(cells);
      } else {
        blocks.push({ kind: "table", rows: [cells] });
      }
    } else {
      const prev = blocks[blocks.length - 1];
      if (prev && prev.kind === "text") {
        prev.lines.push(line);
      } else {
        blocks.push({ kind: "text", lines: [line] });
      }
    }
  }
  return blocks;
}

export function SearchExcerpt({ text, query = "" }: { text: string; query?: string }) {
  const [expanded, setExpanded] = useState(false);
  const isLong = text.length > COLLAPSE_CHARS;
  const visible = isLong && !expanded ? `${text.slice(0, COLLAPSE_CHARS).trimEnd()}…` : text;
  const terms = query
    .trim()
    .split(/\s+/)
    .filter((t) => t.length >= 3);
  const blocks = parseBlocks(visible);

  return (
    <div className="docs__excerpt">
      {blocks.map((block, idx) =>
        block.kind === "table" ? (
          <div key={idx} className="docs__mini-table-wrap">
            <table className="docs__mini-table">
              <tbody>
                {block.rows.map((row, rIdx) => (
                  <tr key={rIdx}>
                    {row.map((cell, cIdx) => (
                      <td key={cIdx}>{highlightText(cell, terms)}</td>
                    ))}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : (
          <div key={idx} className="docs__excerpt-lines">
            {block.lines.map((line, lIdx) => (
              <p key={lIdx} className="docs__excerpt-line">
                {highlightText(line, terms)}
              </p>
            ))}
          </div>
        ),
      )}
      {isLong && (
        <button
          type="button"
          className="docs__expand-btn"
          onClick={() => setExpanded((prev) => !prev)}
        >
          {expanded ? "Show less" : "Show full chunk"}
        </button>
      )}
    </div>
  );
}
