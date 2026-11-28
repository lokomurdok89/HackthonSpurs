import type { ComponentType, SVGProps } from "react";
import { BoxIcon, CheckListIcon, FileIcon, KeyIcon, QrIcon, SearchIcon, SettingsIcon } from "./icons";

export type RouteId = "products" | "review" | "documents" | "search" | "passports" | "access" | "settings";

export interface NavItem {
  id: RouteId;
  label: string;
  icon: ComponentType<SVGProps<SVGSVGElement> & { size?: number }>;
  /** Small count shown next to the label, e.g. items waiting for review. */
  badge?: number;
}

export const mainNav: NavItem[] = [
  { id: "products", label: "Products", icon: BoxIcon },
  { id: "review", label: "Review queue", icon: CheckListIcon },
  { id: "documents", label: "Documents", icon: FileIcon },
  { id: "search", label: "Search", icon: SearchIcon },
  { id: "passports", label: "Passports", icon: QrIcon },
  { id: "access", label: "Buyer access", icon: KeyIcon },
];

/** The sidebar groups the main sections by job: building the evidence, then sharing it. */
export const navGroups: { label: string; items: NavItem[] }[] = [
  { label: "Evidence", items: mainNav.slice(0, 4) },
  { label: "Sharing", items: mainNav.slice(4) },
];

export const secondaryNav: NavItem[] = [{ id: "settings", label: "Settings", icon: SettingsIcon }];

export const allNav = [...mainNav, ...secondaryNav];
