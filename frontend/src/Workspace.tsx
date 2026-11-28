import { useEffect } from "react";
import type { WorkspaceUser } from "./api/auth";
import { AppShell } from "./components/AppShell";
import { allNav } from "./components/nav";
import { PassportView } from "./components/PassportView";
import { useDemoData } from "./data";
import { sampleData } from "./data/sample";
import { setActor } from "./data/settings";
import { BuyerAccess } from "./pages/BuyerAccess";
import { Documents } from "./pages/Documents";
import { DocumentsSearch } from "./pages/DocumentsSearch";
import { Passports } from "./pages/Passports";
import { ProductEvidence } from "./pages/ProductEvidence";
import { ReviewQueue } from "./pages/ReviewQueue";
import { Settings } from "./pages/Settings";
import { Users } from "./pages/Users";
import type { WorkspaceRoute } from "./WorkspaceGate";

interface WorkspaceProps {
  route: WorkspaceRoute;
  user: WorkspaceUser;
  accountChanged: (user: WorkspaceUser, ownPasswordReset: boolean) => void;
  onSignOut: () => Promise<void>;
  signingOut: boolean;
  signOutProblem: string;
}

/** Render the authorized route and expose demo mutations only to the supplied administrator. */
export function Workspace({ route, user, accountChanged, onSignOut, signingOut, signOutProblem }: WorkspaceProps) {
  const demo = useDemoData();
  const { id } = route;
  const label = allNav.find((n) => n.id === id)?.label ?? "";
  const reviewCount = demo.review.reduce((n, item) => n + item.fields.length, 0);
  const canManage = user.role === "admin";

  useEffect(() => setActor(user.displayName), [user.displayName]);
  useEffect(() => { document.title = `${label} · Origin Vault`; }, [label]);

  return (
    <AppShell active={id} badges={{ review: reviewCount }} user={user} onSignOut={onSignOut} signingOut={signingOut}>
      {signOutProblem && <p className="notice" role="alert">{signOutProblem}</p>}
      {id === "products" || id === "review" ? (
        <p className="notice">Demo workspace, {demo.label}. Not derived from your uploads.</p>
      ) : null}
      {id === "documents" ? (
        <Documents documentId={route.documentId} page={route.documentPage} canUpload={canManage} />
      ) : id === "search" ? (
        <DocumentsSearch key={route.documentId} documentId={route.documentId} />
      ) : id === "products" ? (
        <ProductEvidence product={demo.product} manufacturer={demo.passport.manufacturer} canEdit={canManage} />
      ) : id === "review" ? (
        <ReviewQueue items={demo.review} canReview={canManage} />
      ) : id === "passports" ? (
        <>
          <p className="notice">Synthetic passport preview. No upload or private workspace data is shared.</p>
          {canManage ? <Passports passport={sampleData.passport} /> : <PassportView passport={sampleData.passport} />}
        </>
      ) : id === "access" ? (
        canManage ? <BuyerAccess passport={sampleData.passport} />
          : <p role="alert">Only administrators can manage buyer access.</p>
      ) : id === "settings" ? (
        <Settings passport={sampleData.passport} canManage={canManage} />
      ) : id === "users" ? (
        canManage ? <Users currentUser={user} onAccountChanged={accountChanged} />
          : <p role="alert">Only administrators can manage users.</p>
      ) : (
        <>
          <h1 style={{ margin: "0 0 8px", fontSize: 26 }}>{label}</h1>
          <p style={{ margin: 0, color: "var(--muted)" }}>This screen is not built yet.</p>
        </>
      )}
    </AppShell>
  );
}
