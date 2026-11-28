import type { ComponentType, SVGProps } from "react";
import { BoxIcon, CheckListIcon, FileIcon, KeyIcon, QrIcon, SettingsIcon } from "./icons";

export type RouteId = "products" | "review" | "documents" | "passports" | "access" | "settings";

export interface NavItem {
  id: RouteId;
  label: string;
  icon: ComponentType<SVGProps<SVGSVGElement> & { size?: number }>;
  /** Small count shown next to the label, e.g. items waiting for review. */
  badge?: number;
}

export const mainNav: NavItem[] = [
  { id: "products", label: "Products", icon: BoxIcon },
  { id: "review", label: "Review queue", icon: CheckListIcon, badge: 14 },
  { id: "documents", label: "Documents", icon: FileIcon },
  { id: "passports", label: "Passports", icon: QrIcon },
  { id: "access", label: "Buyer access", icon: KeyIcon },
];

export const secondaryNav: NavItem[] = [{ id: "settings", label: "Settings", icon: SettingsIcon }];

export const allNav = [...mainNav, ...secondaryNav];
