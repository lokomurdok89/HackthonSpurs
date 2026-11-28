import type { ReactNode } from "react";
import { mainNav } from "../components/nav";
import { updateSettings, useSettings, type Density, type ThemeChoice } from "../data/settings";

type RowProps = { label: string; help: string; children: ReactNode };

const THEMES: { value: ThemeChoice; label: string }[] = [
  { value: "system", label: "Match my system" },
  { value: "light", label: "Light" },
  { value: "dark", label: "Dark" },
];

const DENSITIES: { value: Density; label: string; help: string }[] = [
  { value: "comfortable", label: "Comfortable", help: "More space between rows" },
  { value: "compact", label: "Compact", help: "More rows on screen" },
];

function Radios<T extends string>({
  name,
  legend,
  options,
  value,
  onChange,
}: {
  name: string;
  legend: string;
  options: { value: T; label: string; help?: string }[];
  value: T;
  onChange: (v: T, label: string) => void;
}) {
  return (
    <fieldset className="st__radios">
      <legend className="visually-hidden">{legend}</legend>
      {options.map((o) => (
        <label key={o.value}>
          <input type="radio" name={name} checked={value === o.value} onChange={() => onChange(o.value, o.label)} />
          <span>
            {o.label}
            {o.help ? <span className="muted small st__opt-help">{o.help}</span> : null}
          </span>
        </label>
      ))}
    </fieldset>
  );
}

export function AppearanceSettings({ Row }: { Row: (p: RowProps) => ReactNode }) {
  const s = useSettings();
  return (
    <>
      <Row label="Theme" help="Match my system follows your computer's light or dark setting. The moon and sun button in the top bar switches it too.">
        <Radios
          name="theme"
          legend="Theme"
          options={THEMES}
          value={s.theme}
          onChange={(v, label) => updateSettings({ theme: v }, `Theme set to ${label.toLowerCase()}`)}
        />
      </Row>
      <Row label="Density" help="Compact tightens tables, review fields and panels so long lists need less scrolling.">
        <Radios
          name="density"
          legend="Density"
          options={DENSITIES}
          value={s.density}
          onChange={(v, label) => updateSettings({ density: v }, `Density set to ${label.toLowerCase()}`)}
        />
      </Row>
      <Row label="Start page" help="The screen Origin Vault opens on when you visit without a link to a specific page.">
        <select
          className="st__select"
          aria-label="Start page"
          value={s.startPage}
          onChange={(e) => {
            const item = mainNav.find((n) => n.id === e.target.value);
            if (item) updateSettings({ startPage: item.id }, `Start page set to ${item.label}`);
          }}
        >
          {mainNav.map((n) => (
            <option key={n.id} value={n.id}>{n.label}</option>
          ))}
        </select>
      </Row>
    </>
  );
}
