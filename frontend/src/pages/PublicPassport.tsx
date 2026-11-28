import { useEffect } from "react";
import { PassportView } from "../components/PassportView";
import type { Passport } from "../data/types";

interface PublicPassportProps {
  passport: Passport;
  productId: string;
  share: number[];
  lots: boolean;
}

/** The buyer-facing page behind the QR code. Shows only the fields encoded in the link. */
export function PublicPassport({ passport, productId, share, lots }: PublicPassportProps) {
  useEffect(() => {
    document.title = `${productId} product passport`;
  }, [productId]);

  if (passport.productId !== productId) {
    return <p style={{ padding: 24 }}>No passport is published for {productId}.</p>;
  }
  const shared = new Set(share.map((i) => passport.fields[i]?.label).filter((l): l is string => !!l));
  return (
    <div style={{ minHeight: "100%", background: "var(--bg)" }}>
      <div style={{ maxWidth: 560, margin: "0 auto", minHeight: "100%" }}>
        <PassportView passport={passport} shared={shared} showLots={lots} />
      </div>
    </div>
  );
}
