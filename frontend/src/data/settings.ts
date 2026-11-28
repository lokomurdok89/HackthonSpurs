import { useEffect, useSyncExternalStore } from "react";
import type { RouteId } from "../components/nav";

export type ThemeChoice = "system" | "light" | "dark";
export type Density = "comfortable" | "compact";

export interface Settings {
  /** Fields at or above this confidence can be approved in one click. 0 turns bulk approve off. */
  autoApproveAt: number;
  /** A corrected value waits for a second reviewer before it counts as approved. */
  secondReviewer: boolean;
  /** Passport field labels switched on when a passport is opened. null means every shareable field. */
  defaultShared: string[] | null;
  defaultShowLots: boolean;
  defaultAccessDays: number;
  theme: ThemeChoice;
  density: Density;
  startPage: RouteId;
}

export interface LogEntry {
  at: string;
  who: string;
  action: string;
  detail: string;
}

interface State {
  settings: Settings;
  log: LogEntry[];
}

const KEY = "origin-vault:settings";
const WHO = "Demo User";

// The top-bar toggle used to save its own key. Carry that choice over once.
function legacyTheme(): ThemeChoice {
  try {
    const v = localStorage.getItem("origin-vault-theme");
    return v === "light" || v === "dark" ? v : "system";
  } catch {
    return "system";
  }
}

const DEFAULTS: Settings = {
  autoApproveAt: 0.95,
  secondReviewer: false,
  defaultShared: null,
  defaultShowLots: true,
  defaultAccessDays: 90,
  theme: legacyTheme(),
  density: "comfortable",
  startPage: "products",
};

function load(): State {
  try {
    const raw = localStorage.getItem(KEY);
    if (raw) {
      const s = JSON.parse(raw) as Partial<State>;
      return { settings: { ...DEFAULTS, ...s.settings }, log: s.log ?? [] };
    }
  } catch {
    /* Storage blocked or corrupt: fall back to defaults. */
  }
  return { settings: DEFAULTS, log: [] };
}

let state = load();
const listeners = new Set<() => void>();

function commit(next: State) {
  state = next;
  try {
    localStorage.setItem(KEY, JSON.stringify(state));
  } catch {
    /* Keep working in memory. */
  }
  listeners.forEach((l) => l());
}

const subscribe = (l: () => void) => {
  listeners.add(l);
  return () => listeners.delete(l);
};

export function useSettings() {
  return useSyncExternalStore(subscribe, () => state.settings);
}

export function useActivityLog() {
  return useSyncExternalStore(subscribe, () => state.log);
}

/** Appends to the activity log. Newest first, capped so storage stays small. */
export function logActivity(action: string, detail: string) {
  const entry: LogEntry = { at: new Date().toISOString(), who: WHO, action, detail };
  commit({ ...state, log: [entry, ...state.log].slice(0, 200) });
}

export function updateSettings(patch: Partial<Settings>, detail: string) {
  const entry: LogEntry = { at: new Date().toISOString(), who: WHO, action: "Changed setting", detail };
  commit({ settings: { ...state.settings, ...patch }, log: [entry, ...state.log].slice(0, 200) });
}

export function resetSettings() {
  updateSettings(DEFAULTS, "Restored default settings");
}

const darkQuery = () => window.matchMedia?.("(prefers-color-scheme: dark)");

/** The theme actually on screen, following the system when the choice is "system". */
export function useResolvedTheme(): "light" | "dark" {
  const { theme } = useSettings();
  const systemDark = useSyncExternalStore(
    (l) => {
      const q = darkQuery();
      q?.addEventListener("change", l);
      return () => q?.removeEventListener("change", l);
    },
    () => !!darkQuery()?.matches,
  );
  return theme === "system" ? (systemDark ? "dark" : "light") : theme;
}

/** Puts theme and density on <html> so every stylesheet can respond. */
export function useApplyAppearance() {
  const { theme, density } = useSettings();
  useEffect(() => {
    const el = document.documentElement;
    if (theme === "system") delete el.dataset.theme;
    else el.dataset.theme = theme;
    el.dataset.density = density;
  }, [theme, density]);
}
