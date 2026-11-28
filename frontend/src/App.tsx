import { useEffect, useState } from "react";
import { allNav } from "./components/nav";
import { sampleData } from "./data/sample";
import { PublicPassport } from "./pages/PublicPassport";
import { WorkspaceGate, type WorkspaceRoute } from "./WorkspaceGate";

type Route = WorkspaceRoute
  | { kind: "public-passport"; productId: string; share: number[]; lots: boolean };

/** Resolve workspace, receipt, page, or demo passport from the browser hash.
 * @param hash - Current browser hash including optional query parameters.
 */
function parseHash(hash: string): Route {
  const raw = hash.replace(/^#\/?/, "");
  const [path, query = ""] = raw.split("?");
  const parts = path.split("/");

  // Buyer-facing link opened from the QR code: #/p/<productId>?share=0,2&lots=1
  if (parts[0] === "p" && parts[1]) {
    const q = new URLSearchParams(query);
    const share = (q.get("share") ?? "")
      .split(",")
      .filter((s) => s !== "")
      .map(Number)
      .filter(Number.isInteger);
    try {
      return { kind: "public-passport", productId: decodeURIComponent(parts[1]), share, lots: q.get("lots") !== "0" };
    } catch {
      return { kind: "app", id: "products", documentId: "" };
    }
  }

  const id = parts[0];
  const page = parts[2] === "page" && /^(?:[1-9]|1[0-9]|20)$/.test(parts[3] ?? "") ? Number(parts[3]) : undefined;
  return {
    kind: "app",
    id: allNav.find((n) => n.id === id)?.id ?? "products",
    documentId: parts[1] ?? "",
    documentPage: page,
  };
}

/** Track hash navigation without issuing HTTP requests. */
function useRoute() {
  const [hash, setHash] = useState(() => window.location.hash);
  useEffect(() => {
    /** Update the current route from the browser hash. */
    const onHash = () => setHash(window.location.hash);
    window.addEventListener("hashchange", onHash);
    return () => window.removeEventListener("hashchange", onHash);
  }, []);
  return parseHash(hash);
}

/** Render the active workspace or isolated public demo passport. */
export default function App() {
  const route = useRoute();

  if (route.kind === "public-passport") {
    return <PublicPassport passport={sampleData.passport} productId={route.productId} share={route.share} lots={route.lots} />;
  }
  return <WorkspaceGate route={route} />;
}
