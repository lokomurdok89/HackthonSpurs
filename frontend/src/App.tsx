import { useEffect, useState } from "react";
import { AppShell } from "./components/AppShell";
import { allNav, type RouteId } from "./components/nav";
import type { CurrentUser } from "./components/UserCard";
import { useDemoData } from "./data";
import { Documents } from "./pages/Documents";
import { Passports } from "./pages/Passports";
import { ProductEvidence } from "./pages/ProductEvidence";
import { PublicPassport } from "./pages/PublicPassport";
import { ReviewQueue } from "./pages/ReviewQueue";

// Placeholder until sign-in exists.
const currentUser: CurrentUser = { name: "Demo User", role: "Trade compliance" };

type Route =
  | { kind: "app"; id: RouteId; documentId: string }
  | { kind: "public-passport"; productId: string; share: number[]; lots: boolean };

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
    return { kind: "public-passport", productId: decodeURIComponent(parts[1]), share, lots: q.get("lots") !== "0" };
  }

  const id = parts[0];
  return {
    kind: "app",
    id: allNav.some((n) => n.id === id) ? (id as RouteId) : "products",
    documentId: parts[1] ?? "",
  };
}

function useRoute() {
  const [hash, setHash] = useState(() => window.location.hash);
  useEffect(() => {
    const onHash = () => setHash(window.location.hash);
    window.addEventListener("hashchange", onHash);
    return () => window.removeEventListener("hashchange", onHash);
  }, []);
  return parseHash(hash);
}

export default function App() {
  const demo = useDemoData();
  const route = useRoute();
  const label = route.kind === "app" ? (allNav.find((n) => n.id === route.id)?.label ?? "") : "";
  const reviewCount = demo.review.reduce((n, item) => n + item.fields.length, 0);

  useEffect(() => {
    if (route.kind === "app") document.title = `${label} · Origin Vault`;
  }, [route.kind, label]);

  if (route.kind === "public-passport") {
    return <PublicPassport passport={demo.passport} productId={route.productId} share={route.share} lots={route.lots} />;
  }

  const { id } = route;
  return (
    <AppShell active={id} badges={{ review: reviewCount }} user={currentUser}>
      {id === "products" || id === "review" ? (
        <p className="notice">Demo workspace, {demo.label}. Not derived from your uploads.</p>
      ) : null}
      {id === "documents" ? (
        <Documents documentId={route.documentId} />
      ) : id === "products" ? (
        <ProductEvidence product={demo.product} />
      ) : id === "review" ? (
        <ReviewQueue items={demo.review} />
      ) : id === "passports" ? (
        <Passports passport={demo.passport} />
      ) : (
        <>
          <h1 style={{ margin: "0 0 8px", fontSize: 26 }}>{label}</h1>
          <p style={{ margin: 0, color: "var(--muted)" }}>This screen is not built yet.</p>
        </>
      )}
    </AppShell>
  );
}
