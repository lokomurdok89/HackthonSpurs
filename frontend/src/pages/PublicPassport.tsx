import { useEffect } from "react";
import { PassportView } from "../components/PassportView";
import type { Passport } from "../data/types";

interface PublicPassportProps {
  passport: Passport;
  productId: string;
  share: number[];
  lots: boolean;
}

/** Render a synthetic passport when its product matches the URL and limit output to requested eligible fields and lot visibility. */
export function PublicPassport({ passport, productId, share, lots }: PublicPassportProps) {
  useEffect(() => {
    document.title = `${productId} product passport`;
  }, [productId]);

  if (passport.productId !== productId) {
    return <p style={{ padding: 24 }}>No passport is published for {productId}.</p>;
  }
  const shared = new Set(share.flatMap((i) => {
    const field = passport.fields[i];
    return field?.status === "verified" || field?.status === "missing" ? [field.label] : [];
  }));
  return (
    <div style={{ minHeight: "100%", background: "var(--bg)" }}>
      <div style={{ maxWidth: 560, margin: "0 auto", minHeight: "100%" }}>
        <PassportView passport={passport} shared={shared} showLots={lots} />
      </div>
    </div>
  );
}
