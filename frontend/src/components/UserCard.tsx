import { MoreIcon } from "./icons";

export interface CurrentUser {
  name: string;
  role: string;
}

function initials(name: string) {
  return name
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((p) => p[0]!.toUpperCase())
    .join("");
}

export function UserCard({ user }: { user: CurrentUser }) {
  return (
    <div className="user-card">
      <span className="user-card__avatar" aria-hidden="true">
        {initials(user.name)}
      </span>
      <span className="user-card__text">
        <span className="user-card__name">{user.name}</span>
        <span className="user-card__role">{user.role}</span>
      </span>
      <button type="button" className="icon-btn icon-btn--dark" aria-label={`Account menu for ${user.name}`}>
        <MoreIcon />
      </button>
    </div>
  );
}
