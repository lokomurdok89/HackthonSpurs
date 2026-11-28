import { backendMessages, unknownProblem } from "./backendMessages.generated";
import type { DocumentStatus, Problem, ReadResult, Receipt } from "./types";
import { ApiError } from "./errors";
import { mockApi } from "./mock";
import { workspaceFetch } from "./workspaceFetch";

export { ApiError };

export interface DocumentApi {
  /** Submit one file under a stable operation key and return its receipt.
   * @param file - File selected for this submission.
   * @param idempotencyKey - Client UUID retained for retries.
   * @param spur - Whether this action requests SPUR OCR instead of local storage.
   */
  upload(file: File, idempotencyKey: string, spur?: boolean): Promise<Receipt>;
  /** Fetch the latest processing state without scheduling work.
   * @param id - Document receipt identifier.
   * @param signal - Optional cancellation signal.
   */
  status(id: string, signal?: AbortSignal): Promise<DocumentStatus>;
  /** Fetch the published reading for a completed document.
   * @param id - Document receipt identifier.
   * @param signal - Optional cancellation signal.
   */
  result(id: string, signal?: AbortSignal): Promise<ReadResult>;
}

/** Decode a safe problem code or fall back when the response is not a problem document.
 * @param res - Unsuccessful HTTP response.
 */
export async function toProblem(res: Response): Promise<Problem> {
  try {
    const body = (await res.json()) as Partial<Problem>;
    if (typeof body.code === "string") {
      return { status: res.status, code: body.code, title: body.title ?? "Request failed" };
    }
  } catch {
    // fall through to a generic problem
  }
  return { status: res.status, code: "unknown_error", title: "Request failed" };
}

/** Fetch same-origin JSON and convert unsuccessful responses into API errors.
 * @param url - Same-origin API path.
 * @param signal - Optional cancellation signal.
 */
export async function getJson<T>(url: string, signal?: AbortSignal): Promise<T> {
  const res = await workspaceFetch(url, { signal, headers: { Accept: "application/json" } });
  if (!res.ok) throw new ApiError(await toProblem(res));
  return (await res.json()) as T;
}

const httpApi: DocumentApi = {
  /** Submit one file under a stable operation key and return its receipt.
   * @param file - File selected for this submission.
   * @param idempotencyKey - Client UUID retained for retries.
   * @param spur - Whether this action requests SPUR OCR instead of local storage.
   */
  async upload(file, idempotencyKey, spur = false) {
    const data = new FormData();
    // Normalize browser MIME labels; the API still checks the submitted bytes.
    const media: Record<string, string> = { csv: "text/csv", txt: "text/plain", pdf: "application/pdf",
      docx: "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
      xlsx: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
      pptx: "application/vnd.openxmlformats-officedocument.presentationml.presentation",
      png: "image/png", jpg: "image/jpeg", jpeg: "image/jpeg" };
    const type = media[file.name.split(".").pop()?.toLowerCase() ?? ""] ?? file.type;
    data.append("file", type === file.type ? file : file.slice(0, file.size, type), file.name);
    // Let the browser set the multipart boundary.
    const res = await workspaceFetch("/api/documents", {
      method: "POST",
      headers: { "Idempotency-Key": idempotencyKey, ...(spur ? { "X-OCR-Provider": "spur" } : {}) },
      body: data,
    });
    if (res.status !== 202) throw new ApiError(await toProblem(res));
    return (await res.json()) as Receipt;
  },
  /** Fetch the latest processing state without scheduling work.
   * @param id - Document receipt identifier.
   * @param signal - Optional cancellation signal.
   */
  status: (id, signal) => getJson<DocumentStatus>(`/api/documents/${encodeURIComponent(id)}`, signal),
  /** Fetch the published reading for a completed document.
   * @param id - Document receipt identifier.
   * @param signal - Optional cancellation signal.
   */
  result: (id, signal) => getJson<ReadResult>(`/api/documents/${encodeURIComponent(id)}/result`, signal),
};

const mode = import.meta.env.VITE_API_MODE ?? (import.meta.env.MODE === "development" ? "mock" : "http");
export const apiMode = mode === "http" ? "http" : "mock";
export const api: DocumentApi = apiMode === "http" ? httpApi : mockApi;

/** Map stable problem codes to safe UI messages; unknown codes get a generic message.
 * @param code - Machine-readable problem code.
 */
export function describeProblem(code: string): string {
  return backendMessages.get(code) ?? unknownProblem;
}
