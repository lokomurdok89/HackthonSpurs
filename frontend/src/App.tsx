import { useEffect, useState } from "react";
import { AppShell } from "./components/AppShell";
import { allNav, type RouteId } from "./components/nav";
import type { CurrentUser } from "./components/UserCard";
import { demo } from "./data";
import { ProductEvidence } from "./pages/ProductEvidence";
import { ReviewQueue } from "./pages/ReviewQueue";

// Placeholder until sign-in exists.
const currentUser: CurrentUser = { name: "Demo User", role: "Trade compliance" };

function routeFromHash(): RouteId {
  const id = window.location.hash.replace(/^#\/?/, "").split("/")[0];
  return allNav.some((n) => n.id === id) ? (id as RouteId) : "products";
}

function useRoute() {
  const [route, setRoute] = useState<RouteId>(routeFromHash);
  useEffect(() => {
    const onHash = () => setRoute(routeFromHash());
    window.addEventListener("hashchange", onHash);
    return () => window.removeEventListener("hashchange", onHash);
  }, []);
  return route;
}

export default function App() {
  const route = useRoute();
  const label = allNav.find((n) => n.id === route)?.label ?? "";

  useEffect(() => {
    document.title = `${label} · Origin Vault`;
  }, [label]);

  return (
    <AppShell active={route} user={currentUser}>
      {route === "products" ? (
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
