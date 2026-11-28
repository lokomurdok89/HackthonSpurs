import { getJson, toProblem } from "./client";
import { ApiError } from "./errors";
import { workspaceFetch } from "./workspaceFetch";
import type { DocumentDetailView, DocumentIndexStatus, SearchStats, SemanticSearchResponse } from "./types";

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
 * @param topK - Maximum number of ranked excerpts requested.
 * @param signal - Cancellation for a view that is being closed.
 */
export async function semanticSearch(query: string, documentId: string, topK: number, signal?: AbortSignal): Promise<SemanticSearchResponse> {
  const response = await workspaceFetch("/api/search", {
    method: "POST",
    signal,
    headers: { "Content-Type": "application/json", Accept: "application/json" },
    body: JSON.stringify({ query, topK, documentId: documentId || undefined }),
  });
  if (!response.ok) throw new ApiError(await toProblem(response));
  return (await response.json()) as SemanticSearchResponse;
}

/** Read the stored index excerpts for receipt id, cancelled by signal on navigation. */
export function getDocumentDetail(id: string, signal?: AbortSignal): Promise<DocumentDetailView> {
  return getJson(`/api/documents/${encodeURIComponent(id)}/chunks`, signal);
}

/** Queue embeddings from the existing transcript for receipt id; never uploads the original. */
export async function reindexDocument(id: string) {
  const response = await workspaceFetch(`/api/documents/${encodeURIComponent(id)}/reindex`, { method: "POST" });
  if (!response.ok) throw new ApiError(await toProblem(response));
}
