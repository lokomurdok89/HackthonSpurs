import type { DocumentApi } from "./client";
import type { DocumentStatus, ReadResult } from "./types";
import { ApiError } from "./errors";

// In-browser stand-in for UI development without a running API.
// Mirrors the contract's states and errors with invented content only.

interface MockJob {
  id: string;
  name: string;
  digest: string;
  createdAt: number;
}

const jobs = new Map<string, MockJob>();
const byKey = new Map<string, string>();
const MAX_BYTES = 20 * 1024 * 1024;

/** Derive synthetic job progress from the fixture name and submission time.
 * @param job - In-memory fixture job.
 */
function stateFor(job: MockJob): DocumentStatus {
  const age = Date.now() - job.createdAt;
  if (job.name.toLowerCase().includes("corrupt")) {
    return age < 3000
      ? { id: job.id, state: "Processing", attempt: 1 }
      : { id: job.id, state: "Failed", attempt: 3, failureCode: "unreadable_file" };
  }
  if (age < 1500) return { id: job.id, state: "Queued", attempt: 0 };
  if (age < 5000) return { id: job.id, state: "Processing", attempt: 1 };
  return { id: job.id, state: "Completed", attempt: 1 };
}

export const mockApi: DocumentApi = {
  /** Create or replay an in-memory receipt for a synthetic upload.
   * @param file - File selected for this submission.
   * @param key - Mock operation key used for replay.
   */
  async upload(file, key) {
    const ext = file.name.split(".").pop()?.toLowerCase();
    if (!ext || !["pdf", "xlsx", "csv"].includes(ext)) {
      throw new ApiError({ status: 415, code: "unsupported_media_type", title: "Unsupported Media Type" });
    }
    if (!file.size) throw new ApiError({ status: 400, code: "invalid_request", title: "Empty file" });
    if (file.size > MAX_BYTES) {
      throw new ApiError({ status: 413, code: "file_too_large", title: "Content Too Large" });
    }
    const hash = await crypto.subtle.digest("SHA-256", await file.arrayBuffer());
    const digest = Array.from(new Uint8Array(hash)).join(",");
    const existing = byKey.get(key);
    if (existing) {
      const job = jobs.get(existing)!;
      if (job.digest !== digest) {
        throw new ApiError({ status: 409, code: "idempotency_conflict", title: "Conflict" });
      }
      return { id: job.id };
    }
    const id = crypto.randomUUID();
    jobs.set(id, { id, name: file.name, digest, createdAt: Date.now() });
    byKey.set(key, id);
    return { id };
  },

  /** Return simulated progress for a known mock job.
   * @param id - Document receipt identifier.
   */
  async status(id) {
    const job = jobs.get(id);
    if (!job) throw new ApiError({ status: 404, code: "document_not_found", title: "Not Found" });
    return stateFor(job);
  },

  /** Return invented fragments only after the mock job reaches completion.
   * @param id - Document receipt identifier.
   */
  async result(id): Promise<ReadResult> {
    const job = jobs.get(id);
    if (!job) throw new ApiError({ status: 404, code: "document_not_found", title: "Not Found" });
    const s = stateFor(job);
    if (s.state === "Failed") throw new ApiError({ status: 409, code: "processing_failed", title: "Conflict" });
    if (s.state !== "Completed") throw new ApiError({ status: 409, code: "result_not_ready", title: "Conflict" });
    const isCsv = /\.csv$/i.test(job.name);
    const isSheet = /\.xlsx$/i.test(job.name);
    return {
      documentId: id,
      version: "1",
      readerVersion: "mock-0.1",
      fragments: isCsv ? [{ method: "native", locator: { kind: "csvCell", row: 2, column: 1 }, text: "CAS-014" }] : isSheet
        ? [
          { method: "native", locator: { kind: "cells", sheet: "Materiales", range: "A12:D12" }, text: "CAS-014 | Bushing, machined | CERT-027 | 2" },
        ]
        : [
          { method: "native", locator: { kind: "page", page: 1 }, text: "Certificate CERT-027 · Part CAS-014 · Grade 4140" },
          { method: "ocr", locator: { kind: "page", page: 2 }, text: "Supplier address: 100 Example Rd, Example City" },
        ],
      warnings: isCsv || isSheet
        ? []
        : [{ code: "partial_page", message: "Could not read the origin section; review the original page.", locator: { kind: "page", page: 2 } }],
    };
  },
};
