import type { ComponentType, SVGProps } from "react";
import { demo } from "../data";
import { BoxIcon, CheckListIcon, FileIcon, KeyIcon, QrIcon, SettingsIcon } from "./icons";

export type RouteId = "products" | "review" | "documents" | "passports" | "access" | "settings";

export interface NavItem {
  id: RouteId;
  label: string;
  icon: ComponentType<SVGProps<SVGSVGElement> & { size?: number }>;
  /** Small count shown next to the label, e.g. items waiting for review. */
  badge?: number;
}

/** Values waiting in the review queue, from the same data the queue shows. */
function reviewCount() {
  return demo.review.reduce((n, item) => n + item.fields.length, 0);
}

export const mainNav: NavItem[] = [
  { id: "products", label: "Products", icon: BoxIcon },
  { id: "review", label: "Review queue", icon: CheckListIcon, badge: reviewCount() },
  { id: "documents", label: "Documents", icon: FileIcon },
  { id: "passports", label: "Passports", icon: QrIcon },
  { id: "access", label: "Buyer access", icon: KeyIcon },
];

/** The sidebar groups the main sections by job: building the evidence, then sharing it. */
export const navGroups: { label: string; items: NavItem[] }[] = [
  { label: "Evidence", items: mainNav.slice(0, 3) },
  { label: "Sharing", items: mainNav.slice(3) },
];

export const secondaryNav: NavItem[] = [{ id: "settings", label: "Settings", icon: SettingsIcon }];

export const allNav = [...mainNav, ...secondaryNav];
