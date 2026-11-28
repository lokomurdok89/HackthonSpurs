import { useEffect, useRef } from "react";
import { BellIcon, LockIcon, LogoIcon, MenuIcon, SearchIcon } from "./icons";
import { ThemeToggle } from "./ThemeToggle";

interface TopBarProps {
  onToggleSidebar: () => void;
  sidebarOpen: boolean;
}

/** Render branding, the OCR search shortcut and the control for the given sidebar state and toggle action. */
export function TopBar({ onToggleSidebar, sidebarOpen }: TopBarProps) {
  const searchRef = useRef<HTMLAnchorElement>(null);

  // "/" focuses the search shortcut unless the person is already typing in a field.
  useEffect(() => {
    /** Focus the search shortcut on an unmodified slash key event outside editable fields. */
    const onKey = (e: KeyboardEvent) => {
      if (e.key !== "/" || e.metaKey || e.ctrlKey || e.altKey) return;
      const t = e.target as HTMLElement | null;
      if (t && (t.isContentEditable || ["INPUT", "TEXTAREA", "SELECT"].includes(t.tagName))) return;
      e.preventDefault();
      searchRef.current?.focus();
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, []);

  return (
    <header className="topbar">
      <button
        type="button"
        className="icon-btn topbar__menu"
        onClick={onToggleSidebar}
        aria-label={sidebarOpen ? "Close navigation" : "Open navigation"}
        aria-expanded={sidebarOpen}
        aria-controls="sidebar"
      >
        <MenuIcon />
      </button>

      <a href="#/products" className="brand">
        <LogoIcon size={20} className="brand__mark" />
        <span className="brand__name">Origin Vault</span>
      </a>

      <a href="#/search" className="search" ref={searchRef} aria-keyshortcuts="/">
        <SearchIcon size={15} className="search__icon" />
        <span>Search OCR documents</span>
        <kbd className="search__key" aria-hidden="true">/</kbd>
      </a>

      <div className="topbar__right">
        <span className="vault-status" title="Files stay in storage the company controls">
          <LockIcon size={14} />
          <span className="vault-status__text">Local vault</span>
        </span>
        <ThemeToggle />
        <button type="button" className="icon-btn" aria-label="Notifications">
          <BellIcon />
        </button>
      </div>
    </header>
  );
}
