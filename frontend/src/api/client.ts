import type { DocumentStatus, Problem, ReadResult, Receipt } from "./types";
import { ApiError } from "./errors";
import { mockApi } from "./mock";

export { ApiError };

export interface DocumentApi {
  upload(file: File, idempotencyKey: string): Promise<Receipt>;
  status(id: string): Promise<DocumentStatus>;
  result(id: string): Promise<ReadResult>;
}

async function toProblem(res: Response): Promise<Problem> {
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

async function getJson<T>(url: string): Promise<T> {
  const res = await fetch(url, { headers: { Accept: "application/json" } });
  if (!res.ok) throw new ApiError(await toProblem(res));
  return (await res.json()) as T;
}

const httpApi: DocumentApi = {
  async upload(file, idempotencyKey) {
    const data = new FormData();
    data.append("file", file);
    // Let the browser set the multipart boundary.
    const res = await fetch("/api/documents", {
      method: "POST",
      headers: { "Idempotency-Key": idempotencyKey },
      body: data,
    });
    if (res.status !== 202) throw new ApiError(await toProblem(res));
    return (await res.json()) as Receipt;
  },
  status: (id) => getJson<DocumentStatus>(`/api/documents/${encodeURIComponent(id)}`),
  result: (id) => getJson<ReadResult>(`/api/documents/${encodeURIComponent(id)}/result`),
};

/** VITE_API_MODE=http talks to the real API; anything else uses the in-browser mock. */
export const api: DocumentApi = import.meta.env.VITE_API_MODE === "http" ? httpApi : mockApi;
export const apiMode: "http" | "mock" = import.meta.env.VITE_API_MODE === "http" ? "http" : "mock";

/** Safe, user-facing text for a problem code. Unknown codes get a generic message. */
export function describeProblem(code: string): string {
  switch (code) {
    case "file_too_large":
      return "This file is larger than the upload limit (20 MiB).";
    case "unsupported_media_type":
      return "Only PDF, XLSX and CSV files can be read right now.";
    case "idempotency_conflict":
      return "This upload key was already used for a different file. Upload it again as a new submission.";
    case "document_not_found":
      return "This document ID is unknown.";
    case "processing_failed":
      return "Reading this document failed. Open the original to review it by hand.";
    case "result_not_ready":
      return "The reading is not finished yet.";
    default:
      return "Something went wrong. Try again in a moment.";
  }
}

/**
 * Poll status until a terminal state, per docs/api/README.md:
 * start at 2 s, back off on errors or 503, cap at 10 s, stop when aborted.
 */
export async function pollUntilDone(
  id: string,
  onStatus: (s: DocumentStatus) => void,
  signal: AbortSignal,
): Promise<DocumentStatus | undefined> {
  let delay = 2000;
  while (!signal.aborted) {
    try {
      const s = await api.status(id);
      onStatus(s);
      if (s.state === "Completed" || s.state === "Failed") return s;
      delay = 2000;
    } catch (e) {
      if (e instanceof ApiError && e.problem.code === "document_not_found") throw e;
      delay = Math.min(delay * 2, 10000);
    }
    await new Promise<void>((resolve) => {
      const t = setTimeout(resolve, delay);
      signal.addEventListener("abort", () => {
        clearTimeout(t);
        resolve();
      }, { once: true });
    });
  }
  return undefined;
}
