import { useCallback, useEffect, useRef, useState } from "react";
import { getCurrentUser, type WorkspaceUser } from "./api/auth";
import { ApiError } from "./api/errors";
import { invalidateWorkspaceRequests, workspaceUnauthorized } from "./api/workspaceFetch";

type Session =
  | { kind: "checking" }
  | { kind: "guest"; expired?: boolean }
  | { kind: "unavailable" }
  | { kind: "ready"; user: WorkspaceUser };

/** Verify and monitor the session only while the private workspace gate is mounted. */
export function useWorkspaceSession() {
  const [session, setSession] = useState<Session>({ kind: "checking" });
  const pending = useRef<AbortController>(null);
  const identity = useRef<string | undefined>(undefined);

  /** Cancel the session check that belonged to the previous view or cookie. */
  const cancel = useCallback(() => {
    pending.current?.abort();
    pending.current = null;
  }, []);

  /** Lock the workspace, with an explanation when the session expired instead of signing out. */
  const lock = useCallback((expired?: boolean) => {
    cancel();
    invalidateWorkspaceRequests();
    identity.current = undefined;
    setSession({ kind: "guest", expired });
  }, [cancel]);

  /** Accept the supplied verified account and invalidate checks in flight for a previous cookie. */
  const accept = useCallback((user: WorkspaceUser) => {
    cancel();
    const key = `${user.id}:${user.role}`;
    if (key !== identity.current) invalidateWorkspaceRequests();
    identity.current = key;
    setSession({ kind: "ready", user });
  }, [cancel]);

  /** Read the server-side account; hide private content if it can no longer be verified. */
  const check = useCallback(async () => {
    if (pending.current) return;
    const controller = new AbortController();
    pending.current = controller;
    try {
      const user = await getCurrentUser(AbortSignal.any([controller.signal, AbortSignal.timeout(10000)]));
      if (!controller.signal.aborted) accept(user);
    } catch (error) {
      if (controller.signal.aborted) return;
      if (error instanceof ApiError && error.problem.status === 401) lock(identity.current !== undefined);
      else {
        invalidateWorkspaceRequests();
        identity.current = undefined;
        setSession({ kind: "unavailable" });
      }
    } finally {
      if (pending.current === controller) pending.current = null;
    }
  }, [accept, lock]);

  useEffect(() => {
    void check();
    return cancel;
  }, [cancel, check]);

  const verified = session.kind === "ready";
  useEffect(() => {
    if (!verified) return;
    /** Lock an authenticated workspace when any protected API request returns 401. */
    const expire = () => lock(true);
    /** Revalidate before displaying private content again after a tab switch. */
    const recheckVisible = () => { if (!document.hidden) void check(); };
    const timer = window.setInterval(recheckVisible, 60000);
    document.addEventListener("visibilitychange", recheckVisible);
    window.addEventListener("focus", recheckVisible);
    window.addEventListener(workspaceUnauthorized, expire);
    return () => {
      window.clearInterval(timer);
      document.removeEventListener("visibilitychange", recheckVisible);
      window.removeEventListener("focus", recheckVisible);
      window.removeEventListener(workspaceUnauthorized, expire);
    };
  }, [check, lock, verified]);

  return { session, accept, check, lock };
}
