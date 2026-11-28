import type { DocumentStatus, Problem, ReadResult, Receipt } from "./types";
import { ApiError } from "./errors";
import { mockApi } from "./mock";

export { ApiError };

export interface DocumentApi {
  upload(file: File, idempotencyKey: string): Promise<Receipt>;
  status(id: string, signal?: AbortSignal): Promise<DocumentStatus>;
  result(id: string, signal?: AbortSignal): Promise<ReadResult>;
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

async function getJson<T>(url: string, signal?: AbortSignal): Promise<T> {
  const res = await fetch(url, { signal, headers: { Accept: "application/json" } });
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
  status: (id, signal) => getJson<DocumentStatus>(`/api/documents/${encodeURIComponent(id)}`, signal),
  result: (id, signal) => getJson<ReadResult>(`/api/documents/${encodeURIComponent(id)}/result`, signal),
};

const mode = import.meta.env.VITE_API_MODE ?? (import.meta.env.MODE === "api" ? "http" : "mock");
export const apiMode = mode === "http" ? "http" : "mock";
export const api: DocumentApi = apiMode === "http" ? httpApi : mockApi;

/** Safe, user-facing text for a problem code. Unknown codes get a generic message. */
export function describeProblem(code: string): string {
  switch (code) {
    case "file_too_large":
      return "This file is larger than the configured upload limit.";
    case "invalid_request":
      return "Select a non-empty document and try again.";
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
