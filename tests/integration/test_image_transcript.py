"""Exercise Documents -> PostgreSQL queue -> private stub -> persisted plain text."""
import base64
import os
import unittest
import uuid

from playwright.sync_api import expect
import test_document_ui as ui
import test_document_api as api
from ocr_stub import TEXT
from printed_fixtures import pdf, office
from test_document_worker import wait_state

PNG = base64.b64decode("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9WHcuU8AAAAASUVORK5CYII=")


class ImageTranscript(ui.DocumentBrowser):
    @classmethod
    def setUpClass(cls):
        """Substitute a private offline image reader before starting the browser suite."""
        previous = api.ENV.copy()
        cls.addClassCleanup(api.ENV.update, previous)
        cls.addClassCleanup(api.ENV.clear)
        api.ENV.update(COMPOSE_FILE=os.pathsep.join([api.ENV.get("COMPOSE_FILE", "compose.yaml"),
                       "compose.spur.yaml", "tests/integration/compose.ocr-test.yaml"]),
                       SPUR_API_KEY="unused-test-only", SPUR_OCR_MODEL="offline-test",
                       SPUR_OCR_ENDPOINT="https://unused.invalid")
        super().setUpClass()

    def test_image_receipt_transcript_and_reset(self):
        """Check single-click submission, multiline text, replay and clearing a previous transcript on new selection."""
        page = self.browser.new_page()
        self.addCleanup(page.close)
        page.goto(self.url)
        picker = page.locator("#document-file")
        image = {"name": "test.png", "mimeType": "image/png", "buffer": PNG}
        picker.set_input_files(image)
        action = page.get_by_role("button", name="Send to OCR")
        expect(action).to_be_enabled()
        expect(page.get_by_role("checkbox")).to_have_count(0)
        with page.expect_response(lambda r: r.url.endswith("/api/documents") and r.request.method == "POST") as first:
            action.click()
        self.assertEqual(first.value.status, 202)
        key = first.value.request.headers["idempotency-key"]
        doc = first.value.json()["id"]
        transcript = page.get_by_role("textbox", name="OCR transcript")
        expect(transcript).to_have_value(TEXT, timeout=20000)
        self.assertTrue(transcript.evaluate("e => e.readOnly"))
        page.reload()
        expect(transcript).to_have_value(TEXT)
        picker.set_input_files(image)
        expect(transcript).to_have_count(0)
        expect(action).to_be_enabled()
        body, mime = api.form([("file", "test.png", "image/png", PNG)])
        def send(key, consent):
            """Replay the staged multipart body with key and include the provider header only with consent."""
            headers = {"Idempotency-Key": key, "Content-Type": mime}
            if consent:
                headers["X-OCR-Provider"] = "spur"
            return api.DocumentsAPI.http("/api/documents", body, headers)
        self.assertEqual(send(key, True)[2]["id"], doc)
        self.assertEqual(send(key, False)[0], 409)
        self.assertEqual(send(str(uuid.uuid4()), False)[0], 415)
        body, mime = api.form([("file", "lie.png", "image/png", api.PDF)])
        self.assertEqual(send(str(uuid.uuid4()), True)[0], 415)

    def test_printed_document_formats(self):
        """Pass real printable files through admission, conversion, page reading and persistence."""
        for ext, media, data, count in [
                ("pdf", "application/pdf", pdf(2), 2),
                ("txt", "text/plain", b"Demo text with quotes: \"a\"", 1),
                ("csv", "text/csv", api.CSV, 1),
                *[office(ext) for ext in ("docx", "xlsx", "pptx")]]:
            with self.subTest(ext=ext):
                body, mime = api.form([("file", "demo." + ext, media, data)])
                response = api.DocumentsAPI.http("/api/documents", body, {"Content-Type": mime,
                    "Idempotency-Key": str(uuid.uuid4()), "X-OCR-Provider": "spur"})
                self.assertEqual(response[0], 202, response[2])
                doc = response[2]["id"]
                wait_state(doc, "Completed", 1, 30)
                result = api.DocumentsAPI.http("/api/documents/" + doc + "/result")[2]
                self.assertEqual([(f["locator"]["page"], f["text"]) for f in result["fragments"]],
                                 [(n, f"Rendered page {n}") for n in range(1, count + 1)])

    def test_safe_reader_failure_on_screen(self):
        """Publish the safe cause beside the receipt without exposing the provider body or indexing a failure."""
        page = self.browser.new_page()
        self.addCleanup(page.close)
        page.goto(self.url)
        page.locator("#document-file").set_input_files({"name": "fake.png", "mimeType": "image/png",
                                                       "buffer": PNG + b"OCR-CREDENTIAL-FAILURE"})
        with page.expect_response(lambda r: r.url.endswith("/api/documents") and r.request.method == "POST") as receipt:
            page.get_by_role("button", name="Send to OCR").click()
        doc = receipt.value.json()["id"]
        status = wait_state(doc, "Failed", 1)
        self.assertEqual(status["failureCode"], "reader_credentials_rejected")
        expect(page.get_by_text("The OCR provider rejected the server credentials.", exact=False)).to_be_visible()
        expect(page.get_by_text("private simulated provider body", exact=False)).to_have_count(0)
        self.assertEqual(api.DocumentsAPI.http(f"/api/documents/{doc}/index-status")[2]["state"], "NotApplicable")

    def test_pdf_shows_both_pages_on_upload_screen(self):
        """Submit a two-page PDF once and read both persisted page transcripts next to the form."""
        page = self.browser.new_page()
        self.addCleanup(page.close)
        page.goto(self.url)
        page.locator("#document-file").set_input_files({"name": "demo.pdf", "mimeType": "application/pdf", "buffer": pdf(2)})
        expect(page.get_by_role("checkbox")).to_have_count(0)
        page.get_by_role("button", name="Send to OCR").click()
        for n in (1, 2):
            expect(page.get_by_role("textbox", name=f"OCR transcript · Page {n}")).to_have_value(
                f"Rendered page {n}", timeout=30000)

    def test_page_limit_does_not_publish_a_truncated_document(self):
        """Reject a 21-page original rather than silently publishing its first 20 pages."""
        body, mime = api.form([("file", "long.pdf", "application/pdf", pdf(21))])
        receipt = api.DocumentsAPI.http("/api/documents", body, {"Content-Type": mime,
            "Idempotency-Key": str(uuid.uuid4()), "X-OCR-Provider": "spur"})[2]
        status = wait_state(receipt["id"], "Failed", 1)
        self.assertEqual(status["failureCode"], "too_many_pages")


if __name__ == "__main__":
    unittest.main(verbosity=2)
