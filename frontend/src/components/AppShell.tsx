import { useEffect, useState, type ReactNode } from "react";
import { Sidebar, type NavBadges } from "./Sidebar";
import { TopBar } from "./TopBar";
import type { RouteId } from "./nav";
import type { CurrentUser } from "./UserCard";
import "./AppShell.css";

interface AppShellProps {
  active: RouteId;
  badges?: NavBadges;
  user: CurrentUser;
  children: ReactNode;
}

/** Render the top bar and navigation around the active route's children, with the current user and optional count badges. */
export function AppShell({ active, badges, user, children }: AppShellProps) {
  const [open, setOpen] = useState(false);

  // Close the mobile drawer with Escape.
  useEffect(() => {
    if (!open) return;
    /** Close the open navigation drawer if the keyboard event is Escape. */
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && setOpen(false);
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [open]);

  return (
    <div className="shell">
      <a href="#main" className="skip-link">
        Skip to content
      </a>
      <TopBar sidebarOpen={open} onToggleSidebar={() => setOpen((o) => !o)} />
      <div className="shell__body">
        <Sidebar active={active} badges={badges} open={open} user={user} onNavigate={() => setOpen(false)} />
        {open ? <div className="scrim" onClick={() => setOpen(false)} aria-hidden="true" /> : null}
        <main id="main" className="shell__main" tabIndex={-1}>
          {children}
        </main>
      </div>
    </div>
  );
}
