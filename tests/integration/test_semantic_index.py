"""Single upload, page search and lease recovery with disposable PostgreSQL and offline providers."""
import json
import os
import socket
import time
import unittest
import urllib.error
import urllib.request
import uuid

from playwright.sync_api import expect
import test_document_api as api
import test_document_ui as ui
from printed_fixtures import pdf
from test_image_transcript import PNG
from test_document_worker import wait_state

WORKER = "spur-index-worker"
HEADERS = {"Content-Type": "application/json"}


def calls(text):
    """Count synthetic calls containing text by reading the private stand-in's request counters."""
    rows = api.compose("exec", "-T", "embedding-stub", "python", "-c",
                       "import urllib.request;print(urllib.request.urlopen('http://127.0.0.1:8080/requests').read().decode())",
                       capture=True)
    return json.loads(rows).get(text, 0)


def index_state(doc, state, seconds=12):
    """Poll a document index until the expected state or timeout and return its projection."""
    end = time.monotonic() + seconds
    while True:
        status, _, data = api.DocumentsAPI.http(f"/api/documents/{doc}/index-status")
        if status == 200 and data["state"] == state:
            return data
        if time.monotonic() >= end:
            raise AssertionError(f"Expected {state}, received {data}")
        time.sleep(0.15)


class SemanticIndex(ui.DocumentBrowser):
    profiles = (*ui.DocumentBrowser.profiles, "--profile", "semantic-index")

    @classmethod
    def setUpClass(cls):
        """Start the real application and its privately networked synthetic reader and embeddings."""
        previous = api.ENV.copy()
        cls.addClassCleanup(api.ENV.update, previous)
        cls.addClassCleanup(api.ENV.clear)
        api.ENV.update(COMPOSE_FILE=os.pathsep.join([api.ENV.get("COMPOSE_FILE", "compose.yaml"),
                       "compose.spur.yaml", "tests/integration/compose.ocr-test.yaml",
                       "tests/integration/compose.embedding-test.yaml"]),
                       SPUR_API_KEY="unused-test-only",
                       SPUR_OCR_MODEL="offline-test", SPUR_OCR_ENDPOINT="https://unused.invalid",
                       SPUR_BASE_URL="http://embedding-stub:8080/v1", SPUR_EMBED_MODEL="test-model",
                       SPUR_EMBED_VERSION="test-v1", SPUR_EMBED_DIMENSIONS="3", SPUR_EMBEDDINGS_ENABLED="true")
        # Keep the assigned test API port stable through its restart while Vite is running.
        with socket.socket() as sock:
            sock.bind(("127.0.0.1", 0))
            api.ENV["API_PORT"] = str(sock.getsockname()[1])
        super().setUpClass()

    def send(self, name, media, data, key=None, ocr=True):
        """Submit synthetic data under the optional key and return the document UUID."""
        body, mime = api.form([("file", name, media, data)])
        headers = {"Content-Type": mime, "Idempotency-Key": key or str(uuid.uuid4())}
        if ocr:
            headers["X-OCR-Provider"] = "spur"
        code, _, receipt = api.DocumentsAPI.http("/api/documents", body, headers)
        self.assertEqual(code, 202, receipt)
        return receipt["id"]

    def search(self, query, doc=None):
        """Issue an explicit query with optional receipt scope."""
        return api.DocumentsAPI.http("/api/search", json.dumps({"query": query, "documentId": doc}).encode(), HEADERS)

    def test_durable_ocr_publication_replay_and_page_search(self):
        """Recover after restart, deduplicate concurrent claims and open the exact OCR page from a search hit."""
        api.compose("stop", WORKER)
        key = str(uuid.uuid4())
        text = "Rendered page 2"
        before = calls(text)
        original = pdf(2)
        doc = self.send("demo.pdf", "application/pdf", original, key)
        wait_state(doc, "Completed", 1, 25)
        self.assertEqual(index_state(doc, "Queued")["attempt"], 0)
        self.assertEqual(self.send("renamed.pdf", "application/pdf", original, key), doc)
        self.assertEqual(self.search(text, doc)[2]["results"], [])
        self.assertEqual(calls(text), before)
        api.compose("restart", "api")
        api.DocumentsAPI.ready()
        api.compose("up", "-d", "--no-deps", "--scale", WORKER + "=2", WORKER)
        indexed = index_state(doc, "Completed", 20)
        self.assertEqual((indexed["attempt"], indexed["chunkCount"]), (1, 2))
        self.assertEqual(calls(text), before + 1)
        self.assertEqual(self.send("again.pdf", "application/pdf", original, key), doc)
        self.assertEqual(calls(text), before + 1)

        page = self.browser.new_page()
        self.addCleanup(page.close)
        page.goto(self.url.rsplit("#", 1)[0] + f"#/documents/{doc}")
        expect(page.get_by_text("Indexed · 2 excerpts.")).to_be_visible()
        page.get_by_role("link", name="Search this document").click()
        expect(page.locator("input[type=file]")).to_have_count(0)
        page.get_by_role("searchbox", name="Search query").fill(text)
        page.get_by_role("button", name="Search", exact=True).click()
        hit = page.get_by_role("link", name="Open reading · Page 2")
        expect(hit).to_be_visible()
        self.assertTrue(page.locator(".ds__hit").first.get_by_text(text, exact=True).is_visible())
        hit.click()
        transcript = page.get_by_role("textbox", name="OCR transcript · Page 2")
        expect(transcript).to_be_focused()
        expect(transcript).to_have_value(text)
        expect(page.locator("input[type=file]")).to_have_count(1)

    def test_ineligible_or_empty_readings_do_not_embed(self):
        """Local-only and legacy vector data never appear in the live index, and blank OCR is skipped."""
        before = api.DocumentsAPI.http("/api/search/stats")[2]["totalChunks"]
        local = self.send("local.csv", "text/csv", api.CSV, ocr=False)
        self.assertEqual(index_state(local, "NotApplicable")["attempt"], 0)
        api.sql(f"""
            INSERT INTO document_chunks ("Id", "DocumentId", "ChunkIndex", "Locator", "Text", "Model", "Embedding", "CreatedAt")
            VALUES (gen_random_uuid(), '{local}', 0, 'document', 'Legacy fixture', 'test-model', '[1,0.1,0.1]', now());
        """)
        empty = self.send("empty.png", "image/png", PNG + b"INDEX-BLANK")
        wait_state(empty, "Completed", 1)
        state = index_state(empty, "Skipped")
        self.assertEqual((state["attempt"], state["chunkCount"]), (0, 0))
        reading = api.DocumentsAPI.http(f"/api/documents/{empty}/result")[2]
        self.assertEqual(reading["warnings"][0]["code"], "no_readable_text")
        self.assertEqual(self.search("unindexed", empty)[2]["results"], [])
        self.assertEqual(self.search("unindexed", local)[2]["results"], [])
        self.assertEqual(calls("unindexed"), 0)
        self.assertEqual(api.DocumentsAPI.http("/api/search/stats")[2]["totalChunks"], before)

    def test_provider_errors_do_not_modify_reading(self):
        """Keep reader success independent from indexing and don't leak provider response diagnostics."""
        for marker, code in [("INDEX-REJECT", "embedding_provider_rejected"),
                             ("INDEX-INCOMPATIBLE", "embedding_response_invalid")]:
            with self.subTest(marker=marker):
                doc = self.send("rejected.png", "image/png", PNG + marker.encode())
                wait_state(doc, "Completed", 1)
                failed = index_state(doc, "Failed")
                self.assertEqual((failed["attempt"], failed["chunkCount"], failed["failureCode"]), (1, 0, code))
                self.assertEqual(api.DocumentsAPI.http(f"/api/documents/{doc}/result")[2]["fragments"][0]["text"], marker)
        doc = self.send("search.png", "image/png", PNG)
        index_state(doc, "Completed")
        code, _, problem = self.search("SEARCH-REJECT", doc)
        self.assertEqual((code, problem["code"]), (502, "embedding_provider_rejected"))
        self.assertNotIn("provider secret-shaped diagnostic", json.dumps(problem))
        with self.assertRaises(urllib.error.HTTPError) as missing:
            urllib.request.urlopen(urllib.request.Request(api.DocumentsAPI.base + f"/api/documents/{doc}/index",
                                                         data=b"", method="POST"), timeout=5)
        self.assertEqual(missing.exception.code, 404)
        self.assertEqual(self.search(" ", doc)[2]["code"], "invalid_search")
        self.assertEqual(self.search("x" * 1001, doc)[2]["code"], "invalid_search")
        self.assertEqual(self.search("x" * 17000, doc)[0], 413)
        for table, key, field, wrong, correct in [
                ("document_index_jobs", "DocumentId", "EmbeddingVersion", "'retired'", "'test-v1'"),
                ("document_index_jobs", "DocumentId", "Dimensions", "2", "3"),
                ("document_chunks", "DocumentId", "Model", "'retired'", "'test-model'")]:
            with self.subTest(incompatible=field):
                api.sql(f"""UPDATE {table} SET "{field}" = {wrong} WHERE "{key}" = '{doc}'""")
                self.assertEqual(self.search("incompatible profile", doc)[2]["results"], [])
                api.sql(f"""UPDATE {table} SET "{field}" = {correct} WHERE "{key}" = '{doc}'""")
        self.assertEqual(calls("incompatible profile"), 0)

    def test_retries_and_expired_lease_never_publish_twice(self):
        """Recover a crashed attempt without a duplicate generation or stale vector overwrite."""
        api.compose("up", "-d", "--no-deps", "--scale", WORKER + "=2", WORKER)
        retry = self.send("retry.png", "image/png", PNG + b"INDEX-RETRY")
        self.assertEqual(index_state(retry, "Completed", 50)["attempt"], 3)
        self.assertEqual(calls("INDEX-RETRY"), 3)

        doc = self.send("lease.png", "image/png", PNG + b"INDEX-LEASE")
        index_state(doc, "Processing")
        for _ in range(30):
            if calls("INDEX-LEASE"):
                break
            time.sleep(.1)
        self.assertEqual(calls("INDEX-LEASE"), 1)
        api.sql(f"""UPDATE document_index_jobs SET "LeaseUntil" = now() - interval '1 second' WHERE "DocumentId" = '{doc}'""")
        self.assertEqual(index_state(doc, "Completed")["attempt"], 2)
        time.sleep(4.1)
        self.assertEqual(calls("INDEX-LEASE"), 2)
        rows = api.sql(f"""SELECT "Embedding"::text FROM document_chunks WHERE "DocumentId" = '{doc}'""")
        self.assertEqual(rows, "[0.1,1,0.1]")


if __name__ == "__main__":
    unittest.main(verbosity=2)
