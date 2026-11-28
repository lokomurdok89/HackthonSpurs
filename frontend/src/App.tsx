import { useEffect, useState } from "react";
import { AppShell } from "./components/AppShell";
import { allNav, type RouteId } from "./components/nav";
import type { CurrentUser } from "./components/UserCard";
import { useDemoData } from "./data";
import { ProductEvidence } from "./pages/ProductEvidence";
import { Documents } from "./pages/Documents";
import { ReviewQueue } from "./pages/ReviewQueue";

// Placeholder until sign-in exists.
const currentUser: CurrentUser = { name: "Demo User", role: "Trade compliance" };

function routeFromHash(hash: string): RouteId {
  const id = hash.replace(/^#\/?/, "").split("/")[0];
  return allNav.some((n) => n.id === id) ? (id as RouteId) : "products";
}

function useRoute() {
  const [hash, setHash] = useState(() => window.location.hash);
  useEffect(() => {
    const onHash = () => setHash(window.location.hash);
    window.addEventListener("hashchange", onHash);
    return () => window.removeEventListener("hashchange", onHash);
  }, []);
  return { route: routeFromHash(hash), documentId: hash.replace(/^#\/?/, "").split("/")[1] ?? "" };
}

export default function App() {
  const demo = useDemoData();
  const { route, documentId } = useRoute();
  const label = allNav.find((n) => n.id === route)?.label ?? "";

  useEffect(() => {
    document.title = `${label} · Origin Vault`;
  }, [label]);

  return (
    <AppShell active={route} user={currentUser}>
      {(route === "products" || route === "review") && <p className="notice">Demo workspace · {demo.label} · Not derived from your uploads.</p>}
      {route === "documents" ? (
        <Documents documentId={documentId} />
      ) : route === "products" ? (
        <ProductEvidence product={demo.product} />
      ) : route === "review" ? (
        <ReviewQueue items={demo.review} />
      ) : (
        <>
          <h1 style={{ margin: "0 0 8px", fontSize: 26 }}>{label}</h1>
          <p style={{ margin: 0, color: "var(--muted)" }}>This screen is not built yet.</p>
        </>
      )}
    </AppShell>
  );
}
