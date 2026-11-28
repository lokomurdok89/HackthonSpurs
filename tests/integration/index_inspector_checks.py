"""Verify stored-index inspection and rebuild do not submit a document to OCR again."""
import json
import uuid

import test_document_api as api
from playwright.sync_api import expect


def receipt_versions(doc):
    """Return database row identities and MVCC versions for this synthetic receipt, never its contents."""
    key = uuid.UUID(doc)
    return api.sql(f'''SELECT kind, id, version FROM (
        SELECT 'reading' AS kind, "Id" AS id, xmin::text AS version FROM document_jobs WHERE "Id" = '{key}'
        UNION ALL SELECT 'index', "DocumentId", xmin::text FROM document_index_jobs WHERE "DocumentId" = '{key}'
        UNION ALL SELECT 'chunk', "Id", xmin::text FROM document_chunks WHERE "DocumentId" = '{key}'
    ) AS versions ORDER BY kind, id;''').splitlines()


def verify_query_boundary(case, doc):
    """Exercise each document read projection; even POST search must leave persisted row versions alone."""
    before = receipt_versions(doc)
    case.assertEqual(len(before), 4, "Expected one reading, one index and two chunks")
    base = f"/api/documents/{doc}"
    for path in (base, base + "/result", base + "/index-status", base + "/chunks", "/api/search/stats"):
        with case.subTest(projection=path):
            case.assertEqual(api.DocumentsAPI.http(path)[0], 200)
    query = json.dumps({"query": "synthetic query boundary", "documentId": doc}).encode()
    case.assertEqual(api.DocumentsAPI.http("/api/search", query, {"Content-Type": "application/json"})[0], 200)
    case.assertEqual(receipt_versions(doc), before, "A query changed a receipt, index job or chunk")
    return [row for row in before if row.startswith("reading|")]


def verify_inspection(case, page, doc, index_state):
    """Inspect the two-page receipt doc using page, reindex it and assert case sees one read per original page."""
    reading_version = verify_query_boundary(case, doc)
    page.get_by_role("button", name="Inspect index excerpts").click()
    inspector = page.get_by_role("region", name="Document inspector")
    expect(inspector.locator(".docs__chunk")).to_have_count(2)
    expect(inspector.locator(".docs__chunk").first).to_contain_text("Rendered page 1")
    with page.expect_response(lambda r: r.url.endswith("/reindex") and r.request.method == "POST") as action:
        inspector.get_by_role("button", name="Re-index", exact=True).click()
    case.assertEqual(action.value.status, 202)
    case.assertEqual(index_state(doc, "Completed")["chunkCount"], 2)
    case.assertEqual([row for row in receipt_versions(doc) if row.startswith("reading|")], reading_version,
                     "The re-index command must not update the original reading")
    page.get_by_role("button", name="Search indexed documents").click()
    expect(page.get_by_role("searchbox", name="Search query")).to_be_visible()
    code, _, detail = api.DocumentsAPI.http(f"/api/documents/{doc}/chunks")
    case.assertEqual((code, detail["documentId"], [c["chunkIndex"] for c in detail["chunks"]]), (200, doc, [0, 1]))
    output = api.compose("logs", "--no-log-prefix", "spur-reader", capture=True)
    case.assertCountEqual([line.strip() for line in output.splitlines() if line.startswith(doc)], [doc + " 1", doc + " 2"])
