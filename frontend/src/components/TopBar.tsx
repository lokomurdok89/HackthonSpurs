import { BellIcon, LockIcon, LogoIcon, MenuIcon, SearchIcon } from "./icons";

interface TopBarProps {
  onToggleSidebar: () => void;
  sidebarOpen: boolean;
}

export function TopBar({ onToggleSidebar, sidebarOpen }: TopBarProps) {
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
        <LogoIcon size={22} className="brand__mark" />
        <span>Origin Vault</span>
      </a>

      <form className="search" role="search" onSubmit={(e) => e.preventDefault()}>
        <label htmlFor="global-search" className="visually-hidden">
          Search products, parts, suppliers and documents
        </label>
        <SearchIcon size={16} className="search__icon" />
        <input id="global-search" type="search" placeholder="Search parts, suppliers, heats, documents" />
      </form>

      <div className="topbar__right">
        <span className="vault-status" title="Files stay in storage the company controls">
          <LockIcon size={15} />
          <span className="vault-status__text">Company vault · local</span>
        </span>
        <button type="button" className="icon-btn" aria-label="Notifications">
          <BellIcon />
        </button>
      </div>
    </header>
  );
}
