import { useEffect, useId, useRef, useState, type FormEvent } from "react";
import {
  accountLimits, createUser, updateUser, type UpdateUser, type WorkspaceRole, type WorkspaceUser,
} from "../api/auth";
import { describeProblem } from "../api/client";
import { ApiError } from "../api/errors";

interface UserEditorProps {
  account?: WorkspaceUser;
  self: boolean;
  onSaved: (account: WorkspaceUser, passwordChanged: boolean) => void;
  onCancel?: () => void;
}

/** Create an active account or edit the supplied account without including unchanged fields in an update. */
export function UserEditor({ account, self, onSaved, onCancel }: UserEditorProps) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const mounted = useRef(false);
  const roleId = useId();
  const title = account ? "Edit user" : "Create user";
  useEffect(() => {
    mounted.current = true;
    return () => { mounted.current = false; };
  }, []);

  /** Validate the user-entered form fields, save once, then discard the password draft. */
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (busy) return;
    const form = event.currentTarget;
    const fields = new FormData(form);
    const displayName = String(fields.get("displayName")).trim();
    const role = (self ? account?.role : fields.get("role")) as WorkspaceRole;
    const password = String(fields.get("password"));
    setError("");
    setBusy(true);
    try {
      let updated: WorkspaceUser;
      if (account) {
        const active = self ? account.active : fields.get("active") === "on";
        const changes: UpdateUser = {
          ...(displayName !== account.displayName ? { displayName } : {}),
          ...(role !== account.role ? { role } : {}),
          ...(active !== account.active ? { active } : {}),
          ...(password ? { password } : {}),
        };
        if (Object.keys(changes).length === 0) {
          setError("No changes to save.");
          return;
        }
        updated = await updateUser(account.id, changes);
      } else {
        updated = await createUser({ username: String(fields.get("username")), displayName, role, password });
      }
      if (mounted.current) {
        form.reset();
        onSaved(updated, !!account && !!password);
      }
    } catch (reason) {
      if (mounted.current) setError(reason instanceof ApiError
        ? describeProblem(reason.problem.code) : "Changes could not be confirmed. Check the user list before trying again.");
    } finally {
      const input = form.elements.namedItem("password");
      if (input instanceof HTMLInputElement) input.value = "";
      if (mounted.current) setBusy(false);
    }
  }

  return (
    <section className="panel">
      <h2>{title}{account ? `: ${account.username}` : ""}</h2>
      <form className="account-form" aria-label={title} onSubmit={submit}>
        {!account && <>
          <label>Username
            <input name="username" autoComplete="off" maxLength={accountLimits.username} required disabled={busy}
              pattern="[A-Za-z0-9][A-Za-z0-9._\-]{2,63}" aria-describedby="new-user-username-hint" />
          </label>
          <p id="new-user-username-hint" className="muted small">3–64 letters, numbers, dots, underscores or hyphens; start with a letter or number.</p>
        </>}
        <label>Display name
          <input name="displayName" defaultValue={account?.displayName} autoComplete="off"
            maxLength={accountLimits.displayName} required disabled={busy} />
        </label>
        <div className="account-form__field">
          <label htmlFor={roleId}>Role</label>
          <select id={roleId} name="role" defaultValue={account?.role ?? "reader"} disabled={self || busy}>
            <option value="reader">Reader</option>
            <option value="admin">Administrator</option>
          </select>
        </div>
        {account && <label className="account-form__check">
          <input name="active" type="checkbox" defaultChecked={account.active} disabled={self || busy} /> Active
        </label>}
        <label>{account ? "New password" : "Password"}
          <input name="password" type="password" autoComplete="new-password" minLength={accountLimits.minPassword}
            maxLength={accountLimits.password} required={!account} disabled={busy} aria-describedby="workspace-password-hint" />
        </label>
        <p id="workspace-password-hint" className="muted small">12–128 characters.{account ? " Leave blank to keep the current password." : ""}</p>
        {self && <p className="muted small">You cannot deactivate yourself or change your role. Resetting your password signs you out.</p>}
        <div className="account-form__actions">
          <button type="submit" className="btn btn--primary" disabled={busy}>
            {busy ? "Saving…" : account ? "Save changes" : "Create user"}
          </button>
          {onCancel && <button type="button" className="btn" disabled={busy} onClick={onCancel}>Cancel</button>}
        </div>
        {error && <p role="alert">{error}</p>}
      </form>
    </section>
  );
}
