import type { DemoData } from "./types";
import { sampleData } from "./sample";

// If a git-ignored *.local.ts file exists in this folder (the event demo data),
// use it; otherwise fall back to the committed synthetic sample. import.meta.glob
// returns an empty object when no file matches, so the build never breaks.
const local = import.meta.glob<{ default: DemoData }>("./*.local.ts", { eager: true });
const first = Object.values(local)[0];

export const demo: DemoData = first?.default ?? sampleData;
export type * from "./types";
