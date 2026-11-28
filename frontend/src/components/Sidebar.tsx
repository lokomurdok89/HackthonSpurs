import type { NavItem, RouteId } from "./nav";
import { navGroups, secondaryNav } from "./nav";
import { UserCard, type CurrentUser } from "./UserCard";

/** Live counts shown next to a section, e.g. values waiting for review. */
export type NavBadges = Partial<Record<RouteId, number>>;

interface SidebarProps {
  active: RouteId;
  badges?: NavBadges;
  open: boolean;
  user: CurrentUser;
  onNavigate: () => void;
}

/** Render the given navigation items with active route, optional count badges, and the action to run after navigation. */
function NavLinks({
  items,
  active,
  badges,
  onNavigate,
}: {
  items: NavItem[];
  active: RouteId;
  badges?: NavBadges;
  onNavigate: () => void;
}) {
  return (
    <ul className="nav-list">
      {items.map(({ id, label, icon: Icon, badge: fixed }) => {
        const badge = badges?.[id] ?? fixed;
        return (
          <li key={id}>
            <a
              href={`#/${id}`}
              className="nav-link"
              aria-current={active === id ? "page" : undefined}
              onClick={onNavigate}
            >
              <Icon size={18} />
              <span className="nav-link__label">{label}</span>
              {badge ? (
                <span className="nav-link__badge">
                  {badge}
                  <span className="visually-hidden"> waiting</span>
                </span>
              ) : null}
            </a>
          </li>
        );
      })}
    </ul>
  );
}

/** Show navigation and the current user; use the active route, badge counts and drawer state to render it, then invoke the navigation callback. */
export function Sidebar({ active, badges, open, user, onNavigate }: SidebarProps) {
  return (
    <aside id="sidebar" className={`sidebar${open ? " sidebar--open" : ""}`} aria-label="Sections">
      <nav className="sidebar__nav" aria-label="Main">
        {navGroups.map((g) => (
          <div key={g.label} className="nav-group">
            <div className="nav-group__label" id={`nav-${g.label}`}>
              {g.label}
            </div>
            <div aria-labelledby={`nav-${g.label}`} role="group">
              <NavLinks items={g.items} active={active} badges={badges} onNavigate={onNavigate} />
            </div>
          </div>
        ))}
      </nav>
      <div className="sidebar__bottom">
        <nav aria-label="Secondary">
          <NavLinks items={secondaryNav} active={active} onNavigate={onNavigate} />
        </nav>
        <UserCard user={user} />
      </div>
    </aside>
  );
}
