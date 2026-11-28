import { useEffect, useState } from "react";
import { sampleData } from "./sample";
import type { DemoData } from "./types";

// Only opt-in local Vite development may import private demo modules.
// Lazy imports inside the DEV branch are removed entirely from release bundles.
/** Load the sample workspace unless an enabled local development override is available. */
export function useDemoData() {
  const [demo, setDemo] = useState(sampleData);
  useEffect(() => {
    if (!import.meta.env.DEV || import.meta.env.MODE !== "development" ||
        import.meta.env.VITE_LOCAL_DEMO !== "true") return;
    const local = import.meta.glob<{ default: DemoData }>("./*.local.ts");
    const first = Object.values(local)[0];
    let active = true;
    if (first) void first().then(({ default: data }) => {
      if (active) setDemo(data);
    }).catch(() => { /* Keep the explicitly labelled sample on import failure. */ });
    return () => { active = false; };
  }, []);
  return demo;
}
export type * from "./types";
