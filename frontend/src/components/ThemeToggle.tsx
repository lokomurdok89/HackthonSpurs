import { updateSettings, useResolvedTheme } from "../data/settings";
import { MoonIcon, SunIcon } from "./icons";

/** Quick light and dark switch. Settings, Appearance also offers "Match my system". */
export function ThemeToggle() {
  const theme = useResolvedTheme();
  const next = theme === "dark" ? "light" : "dark";
  return (
    <button
      type="button"
      className="icon-btn"
      aria-label={`Switch to ${next} theme`}
      title={`Switch to ${next} theme`}
      onClick={() => updateSettings({ theme: next }, `Theme set to ${next}`)}
    >
      {theme === "dark" ? <SunIcon /> : <MoonIcon />}
    </button>
  );
}
