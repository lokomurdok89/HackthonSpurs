import { useEffect, useState } from "react";
import { listUsers, roleLabel, type WorkspaceUser } from "../api/auth";
import { describeProblem } from "../api/client";
import { ApiError } from "../api/errors";
import { UserEditor } from "./UserEditor";
import "./WorkspaceAccess.css";

interface UsersProps {
  currentUser: WorkspaceUser;
  onAccountChanged: (account: WorkspaceUser, ownPasswordReset: boolean) => void;
}

/** List and maintain workspace accounts while exposing changes that affect the supplied current user. */
export function Users({ currentUser, onAccountChanged }: UsersProps) {
  const [users, setUsers] = useState<WorkspaceUser[]>();
  const [editing, setEditing] = useState<string>();
  const [loadError, setLoadError] = useState("");
  const [notice, setNotice] = useState("");
  const [version, setVersion] = useState(0);
  const target = users?.find((user) => user.id === editing);

  useEffect(() => {
    const controller = new AbortController();
    void listUsers(controller.signal).then((value) => {
      if (!controller.signal.aborted) setUsers(value);
    }).catch((reason) => {
      if (!controller.signal.aborted) setLoadError(reason instanceof ApiError
        ? describeProblem(reason.problem.code) : "Could not load the user list.");
    });
    return () => controller.abort();
  }, [version]);

  /** Update the list with the returned account and notify the gate of a self change. */
  function saved(account: WorkspaceUser, passwordChanged: boolean) {
    setUsers((previous) => [...(previous ?? []).filter((user) => user.id !== account.id), account]
      .sort((a, b) => a.username.localeCompare(b.username, undefined, { sensitivity: "base" })));
    setNotice(target ? "User updated." : "User created.");
    setEditing(undefined);
    if (account.id === currentUser.id) onAccountChanged(account, passwordChanged);
  }

  return (
    <div className="users">
      <header>
        <h1>Users</h1>
        <p className="muted">Administrators manage access and uploads. Readers can consult documents and search indexed OCR.</p>
      </header>
      {notice && <p role="status">{notice}</p>}
      {!users && !loadError && <p role="status">Loading users…</p>}
      {loadError && <div>
        <p role="alert">{loadError}</p>
        <button type="button" className="btn" onClick={() => { setLoadError(""); setVersion((v) => v + 1); }}>Retry list</button>
      </div>}
      {users && <section className="panel users__list" aria-labelledby="users-list-title">
        <h2 id="users-list-title">Workspace users</h2>
        <table aria-labelledby="users-list-title">
          <thead><tr>
            <th scope="col">Username</th><th scope="col">Display name</th><th scope="col">Role</th>
            <th scope="col">Status</th><th scope="col"><span className="visually-hidden">Actions</span></th>
          </tr></thead>
          <tbody>{users.map((user) => <tr key={user.id}>
            <td>{user.username}{user.id === currentUser.id && <span className="muted small"> (you)</span>}</td>
            <td>{user.displayName}</td><td>{roleLabel[user.role]}</td><td>{user.active ? "Active" : "Inactive"}</td>
            <td>
              <button type="button" className="btn btn--sm" aria-label={`Edit ${user.username}`}
                onClick={() => { setNotice(""); setEditing(user.id); }}>Edit</button>
            </td>
          </tr>)}</tbody>
        </table>
      </section>}
      {users && <UserEditor key={target?.id ?? "create"} account={target} self={target?.id === currentUser.id}
        onSaved={saved} onCancel={target ? () => { setEditing(undefined); } : undefined} />}
    </div>
  );
}
