import { useEffect, useState, type FormEvent } from "react";
import { accountLimits, login, type WorkspaceUser } from "../api/auth";
import { describeProblem } from "../api/client";
import { ApiError } from "../api/errors";
import { LockIcon, LogoIcon } from "../components/icons";
import { ThemeToggle } from "../components/ThemeToggle";
import "./WorkspaceAccess.css";

interface SignInProps {
  state: "checking" | "unavailable" | "guest";
  expired?: boolean;
  onSignIn: (user: WorkspaceUser) => void;
  onRetry: () => Promise<void>;
}

/** Present the session check or sign-in form and return only a verified account to the workspace gate. */
export function SignIn({ state, expired, onSignIn, onRetry }: SignInProps) {
  const [busy, setBusy] = useState(false);
  const [problem, setProblem] = useState("");
  useEffect(() => { document.title = "Sign in · Origin Vault"; }, []);

  /** Send the submitted credentials, then discard the password whether or not sign-in succeeds. */
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (busy) return;
    const form = new FormData(event.currentTarget);
    const password = event.currentTarget.elements.namedItem("password");
    setProblem("");
    setBusy(true);
    try {
      onSignIn(await login(String(form.get("username")), String(form.get("password"))));
    } catch (error) {
      setProblem(error instanceof ApiError ? describeProblem(error.problem.code) : "The workspace API could not be reached.");
    } finally {
      if (password instanceof HTMLInputElement) password.value = "";
      setBusy(false);
    }
  }

  return (
    <main className="signin">
      <div className="signin__theme"><ThemeToggle /></div>
      <section className="panel signin__panel" aria-labelledby="signin-title">
        <p className="signin__brand"><LogoIcon size={23} /> Origin Vault</p>
        <h1 id="signin-title">Sign in</h1>
        <p className="muted">Use your workspace account to access documents and evidence.</p>
        {state === "checking" ? <p role="status">Checking your session…</p> : null}
        {state === "unavailable" ? (
          <>
            <p role="alert">The workspace API is unavailable. Your workspace is locked until access can be verified.</p>
            <button type="button" className="btn" onClick={() => void onRetry()}>Check again</button>
          </>
        ) : null}
        {state === "guest" ? (
          <form className="account-form" aria-label="Sign in" onSubmit={submit}>
            {expired && <p className="notice" role="status">Your session ended. Sign in again to continue.</p>}
            <label>Username
              <input name="username" autoComplete="username" maxLength={accountLimits.username} required />
            </label>
            <label>Password
              <input name="password" type="password" autoComplete="current-password" maxLength={accountLimits.password} required />
            </label>
            <button className="btn btn--primary" disabled={busy} type="submit">
              <LockIcon size={14} /> {busy ? "Signing in…" : "Sign in"}
            </button>
            {problem && <p role="alert">{problem}</p>}
          </form>
        ) : null}
      </section>
    </main>
  );
}
