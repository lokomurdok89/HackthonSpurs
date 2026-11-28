import { useEffect, useState } from "react";
import { AppShell } from "./components/AppShell";
import { PassportView } from "./components/PassportView";
import { allNav, type RouteId } from "./components/nav";
import type { CurrentUser } from "./components/UserCard";
import { demo } from "./data";
import { Passports } from "./pages/Passports";
import { ProductEvidence } from "./pages/ProductEvidence";
import { ReviewQueue } from "./pages/ReviewQueue";

// Placeholder until sign-in exists.
const currentUser: CurrentUser = { name: "Demo User", role: "Trade compliance" };

type Route = { kind: "app"; id: RouteId } | { kind: "public-passport"; productId: string; share: number[]; lots: boolean };

function parseHash(): Route {
  const raw = window.location.hash.replace(/^#\/?/, "");
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
  return { kind: "app", id: allNav.some((n) => n.id === id) ? (id as RouteId) : "products" };
}

function useRoute() {
  const [route, setRoute] = useState<Route>(parseHash);
  useEffect(() => {
    const onHash = () => setRoute(parseHash());
    window.addEventListener("hashchange", onHash);
    return () => window.removeEventListener("hashchange", onHash);
  }, []);
  return route;
}

function PublicPassport({ productId, share, lots }: { productId: string; share: number[]; lots: boolean }) {
  const p = demo.passport;
  useEffect(() => {
    document.title = `${productId} · Product passport`;
  }, [productId]);

  if (p.productId !== productId) {
    return <p style={{ padding: 24 }}>No passport is published for {productId}.</p>;
  }
  const shared = new Set(share.map((i) => p.fields[i]?.label).filter((l): l is string => !!l));
  return (
    <div style={{ minHeight: "100%", background: "var(--bg)" }}>
      <div style={{ maxWidth: 520, margin: "0 auto", minHeight: "100%", boxShadow: "0 0 0 1px var(--line)" }}>
        <PassportView passport={p} shared={shared} showLots={lots} />
      </div>
    </div>
  );
}

export default function App() {
  const route = useRoute();
  const label = route.kind === "app" ? (allNav.find((n) => n.id === route.id)?.label ?? "") : "";

  useEffect(() => {
    if (route.kind === "app") document.title = `${label} · Origin Vault`;
  }, [route.kind, label]);

  if (route.kind === "public-passport") {
    return <PublicPassport productId={route.productId} share={route.share} lots={route.lots} />;
  }

  return (
    <AppShell active={route.id} user={currentUser}>
      {route.id === "products" ? (
        <ProductEvidence product={demo.product} />
      ) : route.id === "review" ? (
        <ReviewQueue items={demo.review} />
      ) : route.id === "passports" ? (
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
