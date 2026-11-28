import { ApiError } from "./errors";
import type {
  DocumentDetailView,
  IndexDocumentResponse,
  Problem,
  Receipt,
  SearchStats,
  SemanticSearchResponse,
} from "./types";

async function readProblem(res: Response): Promise<Problem> {
  try {
    const body = (await res.json()) as Partial<Problem>;
    if (typeof body.code === "string") {
      return { status: res.status, code: body.code, title: body.title ?? "Request failed" };
    }
  } catch {
    // fallback below
  }
  return { status: res.status, code: "request_failed", title: `Request failed (${res.status})` };
}

export async function getSearchStats(): Promise<SearchStats> {
  const res = await fetch("/api/search/stats", { headers: { Accept: "application/json" } });
  if (!res.ok) throw new ApiError(await readProblem(res));
  return (await res.json()) as SearchStats;
}

export async function semanticSearch(
  query: string,
  topK = 8,
  documentId?: string,
): Promise<SemanticSearchResponse> {
  const res = await fetch("/api/search", {
    method: "POST",
    headers: { "Content-Type": "application/json", Accept: "application/json" },
    body: JSON.stringify({ query, topK, documentId: documentId || undefined }),
  });
  if (!res.ok) throw new ApiError(await readProblem(res));
  return (await res.json()) as SemanticSearchResponse;
}

export async function uploadAndIndexDocument(file: File): Promise<{
  receipt: Receipt;
  indexed: IndexDocumentResponse;
}> {
  const form = new FormData();
  form.append("file", file);

  const uploadRes = await fetch("/api/documents", {
    method: "POST",
    headers: { "Idempotency-Key": crypto.randomUUID() },
    body: form,
  });
  if (uploadRes.status !== 202) throw new ApiError(await readProblem(uploadRes));
  const receipt = (await uploadRes.json()) as Receipt;

  const indexRes = await fetch(`/api/documents/${encodeURIComponent(receipt.id)}/index`, {
    method: "POST",
    headers: { "Content-Type": "application/json", Accept: "application/json" },
    body: JSON.stringify({ sourceName: file.name }),
  });
  if (!indexRes.ok) throw new ApiError(await readProblem(indexRes));
  const indexed = (await indexRes.json()) as IndexDocumentResponse;

  return { receipt, indexed };
}

export async function getDocumentDetail(id: string): Promise<DocumentDetailView> {
  const res = await fetch(`/api/documents/${encodeURIComponent(id)}/chunks`, {
    headers: { Accept: "application/json" },
  });
  if (!res.ok) throw new ApiError(await readProblem(res));
  return (await res.json()) as DocumentDetailView;
}

export async function reindexDocument(id: string, sourceName?: string | null): Promise<IndexDocumentResponse> {
  const res = await fetch(`/api/documents/${encodeURIComponent(id)}/index`, {
    method: "POST",
    headers: { "Content-Type": "application/json", Accept: "application/json" },
    body: JSON.stringify({ sourceName: sourceName || undefined }),
  });
  if (!res.ok) throw new ApiError(await readProblem(res));
  return (await res.json()) as IndexDocumentResponse;
}
