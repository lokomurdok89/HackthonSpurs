/** Build a synthetic preview link with the product ID, selected field indexes and lot visibility. */
export function publicPassportUrl(productId: string, sharedIdx: number[], lots: boolean) {
  const q = new URLSearchParams({ share: sharedIdx.join(","), lots: lots ? "1" : "0" });
  return `${window.location.origin}${window.location.pathname}#/p/${encodeURIComponent(productId)}?${q}`;
}
