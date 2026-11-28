// Types mirroring docs/api/openapi.json (v0.2.0). The OpenAPI file is the source of truth.

export type Uuid = string;

export interface Receipt {
  id: Uuid;
}

export type JobState = "Queued" | "Processing" | "Completed" | "Failed";

export interface DocumentStatus {
  id: Uuid;
  state: JobState;
  /** Number of claims so far; zero before the first claim. */
  attempt: number;
  /** Present only when state is Failed. */
  failureCode?: string;
}

export type Locator =
  | { kind: "page"; page: number }
  | { kind: "cells"; sheet: string; range: string }
  | { kind: "csvCell"; row: number; column: number };

export interface Fragment {
  method: "native" | "ocr";
  locator: Locator;
  text: string;
}

export interface ReadWarning {
  code: string;
  message: string;
  locator: Locator;
}

export interface ReadResult {
  documentId: Uuid;
  version: "1";
  readerVersion: string;
  fragments: Fragment[];
  warnings: ReadWarning[];
}

export interface Problem {
  status: number;
  code: string;
  title: string;
}

export const ACCEPTED_TYPES = [
  "application/pdf",
  "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
  "text/csv",
] as const;

export const ACCEPT_ATTRIBUTE = ".pdf,.xlsx,.csv";

export type IndexState = JobState | "AwaitingReading" | "NotApplicable" | "NotIndexed" | "Skipped";

export interface DocumentIndexStatus {
  id: Uuid;
  state: IndexState;
  attempt: number;
  chunkCount: number;
  failureCode?: string;
}

export interface SearchStats {
  embeddingsConfigured: boolean;
  model: string;
  totalDocuments: number;
  indexedDocuments: number;
  totalChunks: number;
}

export interface SemanticSearchHit {
  chunkId: Uuid;
  documentId: Uuid;
  chunkIndex: number;
  sourceName: string | null;
  locator: string;
  pageLocator: Extract<Locator, { kind: "page" }>;
  text: string;
  model: string;
  score: number;
}

export interface SemanticSearchResponse {
  query: string;
  model: string;
  results: SemanticSearchHit[];
}
