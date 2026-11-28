import { useState } from "react";
import { logout, type WorkspaceUser } from "./api/auth";
import type { RouteId } from "./components/nav";
import { SignIn } from "./pages/SignIn";
import { useWorkspaceSession } from "./useWorkspaceSession";
import { Workspace } from "./Workspace";

export interface WorkspaceRoute {
  kind: "app";
  id: RouteId;
  documentId: string;
  documentPage?: number;
}

/** Mount the supplied private route only after the server verifies the current account. */
export function WorkspaceGate({ route }: { route: WorkspaceRoute }) {
  const { session, check, accept, lock } = useWorkspaceSession();
  const [leaving, setLeaving] = useState(false);
  const [problem, setProblem] = useState("");

  /** Revoke the cookie first, then unmount workspace data, or report a sign-out failure. */
  async function signOut() {
    if (leaving) return;
    setLeaving(true);
    setProblem("");
    try {
      await logout();
      lock();
    } catch {
      setProblem("Sign out could not be confirmed. Try again.");
    } finally {
      setLeaving(false);
    }
  }

  if (session.kind !== "ready") return (
    <SignIn state={session.kind} expired={session.kind === "guest" && session.expired} onRetry={check} onSignIn={(user) => {
      setProblem("");
      accept(user);
    }} />
  );

  const { user } = session;
  /** Refresh the current identity when the current user edits their account, or lock it after a password change. */
  const accountChanged = (updated: WorkspaceUser, ownPasswordReset: boolean) => {
    if (updated.id !== user.id) return;
    if (ownPasswordReset) lock(true);
    else accept(updated);
  };

  return <Workspace key={`${user.id}:${user.role}`} route={route} user={user} accountChanged={accountChanged}
    onSignOut={signOut} signingOut={leaving} signOutProblem={problem} />;
}
