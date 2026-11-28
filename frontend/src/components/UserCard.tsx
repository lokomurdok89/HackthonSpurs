import { roleLabel, type WorkspaceUser } from "../api/auth";

/** Derive the letters displayed in the user avatar.
 * @param name - User display name.
 */
function initials(name: string) {
  return name
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((p) => p[0]!.toUpperCase())
    .join("");
}

/** Show the current user and expose the supplied sign-out action.
 * @param props - Component inputs.
 * @param props.user - Current user's display details.
 * @param props.onSignOut - Revokes the current browser session.
 * @param props.signingOut - Prevents duplicate sign-out requests.
 */
export function UserCard({ user, onSignOut, signingOut }: {
  user: WorkspaceUser; onSignOut: () => Promise<void>; signingOut: boolean;
}) {
  return (
    <div className="user-card">
      <span className="user-card__avatar" aria-hidden="true">
        {initials(user.displayName)}
      </span>
      <span className="user-card__text">
        <span className="user-card__name" title={user.username}>{user.displayName}</span>
        <span className="user-card__role">{roleLabel[user.role]}</span>
      </span>
      <button type="button" className="user-card__signout" disabled={signingOut} onClick={() => void onSignOut()}>
        {signingOut ? "Signing out…" : "Sign out"}
      </button>
    </div>
  );
}
