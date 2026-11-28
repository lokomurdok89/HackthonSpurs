import type { NavItem, RouteId } from "./nav";
import { mainNav, secondaryNav } from "./nav";
import { UserCard, type CurrentUser } from "./UserCard";

interface SidebarProps {
  active: RouteId;
  open: boolean;
  user: CurrentUser;
  onNavigate: () => void;
}

function NavLinks({ items, active, onNavigate }: { items: NavItem[]; active: RouteId; onNavigate: () => void }) {
  return (
    <ul className="nav-list">
      {items.map(({ id, label, icon: Icon, badge }) => (
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
      ))}
    </ul>
  );
}

export function Sidebar({ active, open, user, onNavigate }: SidebarProps) {
  return (
    <aside id="sidebar" className={`sidebar${open ? " sidebar--open" : ""}`} aria-label="Sections">
      <nav className="sidebar__nav" aria-label="Main">
        <NavLinks items={mainNav} active={active} onNavigate={onNavigate} />
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
