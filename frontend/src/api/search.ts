import { getJson, toProblem } from "./client";
import { ApiError } from "./errors";
import type { DocumentIndexStatus, SearchStats, SemanticSearchResponse } from "./types";

/** Read the durable index state without initiating any work.
 * @param id - Document receipt identifier.
 * @param signal - Cancellation for a view that is being closed.
 */
export function getIndexStatus(id: string, signal?: AbortSignal): Promise<DocumentIndexStatus> {
  return getJson(`/api/documents/${encodeURIComponent(id)}/index-status`, signal);
}

/** Read compatible index counts without contacting the provider.
 * @param signal - Optional cancellation for the active view.
 */
export function getSearchStats(signal?: AbortSignal): Promise<SearchStats> {
  return getJson("/api/search/stats", signal);
}

/** Search the stored OCR excerpts only when explicitly submitted.
 * @param query - User-entered search text.
 * @param documentId - Optional scope for the search.
 * @param signal - Cancellation for a view that is being closed.
 */
export async function semanticSearch(query: string, documentId: string, signal?: AbortSignal): Promise<SemanticSearchResponse> {
  const response = await fetch("/api/search", {
    method: "POST",
    signal,
    headers: { "Content-Type": "application/json", Accept: "application/json" },
    body: JSON.stringify({ query, topK: 8, documentId: documentId || undefined }),
  });
  if (!response.ok) throw new ApiError(await toProblem(response));
  return (await response.json()) as SemanticSearchResponse;
}
