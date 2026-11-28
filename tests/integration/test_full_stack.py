"""Exercise the published React image and same-origin gateway with isolated offline providers."""
import hashlib
import json
import os
import socket
import time
import unittest

from playwright.sync_api import expect, sync_playwright

import test_document_api as api
from printed_fixtures import pdf
from workspace_client import ADMIN, PASSWORD


class FullStack(unittest.TestCase):
    def test_gateway_to_persisted_result(self):
        """Sign in, process a synthetic invoice, recreate the stack, and read the same receipt."""
        files = ["compose.full.yaml", "tests/integration/compose.test-base.yaml",
                 "tests/integration/compose.ocr-test.yaml", "tests/integration/compose.embedding-test.yaml"]
        if os.environ.get("SPURS_TEST_SUBNET"):
            files.append("compose.subnet.yaml")
        previous = api.ENV.copy()
        self.addCleanup(api.ENV.update, previous)
        self.addCleanup(api.ENV.clear)
        with socket.socket() as sock:
            sock.bind(("127.0.0.1", 0))
            port = str(sock.getsockname()[1])
        api.ENV.update(COMPOSE_FILE=os.pathsep.join(files), COMPOSE_PROFILES="", WEB_PORT=port,
                       SPURS_SUBNET=os.environ.get("SPURS_TEST_SUBNET", ""),
                       SPUR_API_KEY="unused-test-only", SPUR_OCR_MODEL="offline-test",
                       SPUR_OCR_ENDPOINT="https://unused.invalid", SPUR_EMBEDDINGS_ENABLED="true",
                       SPUR_BASE_URL="http://embedding-stub:8080/v1", SPUR_EMBED_MODEL="test-model",
                       SPUR_EMBED_VERSION="test-v1", SPUR_EMBED_DIMENSIONS="3")
        self.addCleanup(api.compose, "down", "-v", "--remove-orphans", "--rmi", "local")
        services = json.loads(api.compose("config", "--format", "json", capture=True))["services"]
        for name in ("web", "api", "postgres", "spur-worker", "spur-index-worker", "spur-reader"):
            self.assertEqual(services[name]["restart"], "unless-stopped")
            if name != "web":
                self.assertFalse(services[name].get("ports"))
        self.assertEqual(services["web"]["ports"][0]["host_ip"], "127.0.0.1")
        api.compose("up", "--build", "-d", "--wait", "--wait-timeout", "120")
        base = "http://127.0.0.1:" + port
        with sync_playwright() as driver:
            browser = driver.chromium.launch(channel=os.environ.get("SPURS_BROWSER_CHANNEL"))
            page = browser.new_page()
            page.goto(base + "/#/documents")
            page.get_by_label("Username").fill(ADMIN)
            page.get_by_label("Password", exact=True).fill(PASSWORD)
            page.get_by_role("button", name="Sign in").click()
            media = {"name": "synthetic-invoice.pdf", "mimeType": "application/pdf", "buffer": pdf(1)}
            page.locator("#document-file").set_input_files(media)
            with page.expect_response(lambda r: r.url.endswith("/api/documents") and r.request.method == "POST") as upload:
                page.get_by_role("button", name="Send to OCR").click()
            self.assertEqual(upload.value.status, 202)
            doc = upload.value.json()["id"].replace("-", "")
            receipt = upload.value.headers["location"]
            transcript = page.get_by_role("textbox", name="OCR transcript")
            expect(transcript).to_have_value("Rendered page 1", timeout=45000)
            deadline = time.monotonic() + 30
            while page.request.get(base + receipt + "/index-status").json()["state"] != "Completed":
                self.assertLess(time.monotonic(), deadline, "Index worker did not publish")
                time.sleep(.25)
            # A loopback TLS edge overwrites X-Forwarded-Proto. Never print the cookie.
            edge = driver.request.new_context()
            auth = edge.post(base + "/api/auth/login", headers={"X-Forwarded-Proto": "https",
                                     "X-Workspace-Request": "1"}, data={"username": ADMIN, "password": PASSWORD})
            self.assertEqual(auth.status, 200)
            flags = auth.headers["set-cookie"].lower().split("; ")[1:]
            self.assertIn("secure", flags)
            self.assertIn("httponly", flags)
            edge.dispose()
            api.compose("down")  # Preserve both named volumes; restart with fresh containers.
            api.compose("up", "-d", "--wait", "--wait-timeout", "120")
            page.goto(base + "/#/documents")
            # The original browser session, receipt, original file and index survive replacement.
            expect(page.locator("#document-file")).to_be_visible()
            self.assertEqual(page.request.get(base + "/api/auth/me").status, 200)
            digest = api.compose("exec", "-T", "api", "sha256sum", "/data/originals/" + doc + ".original", capture=True)
            self.assertEqual(digest.split()[0], hashlib.sha256(media["buffer"]).hexdigest())
            result = page.request.get(base + receipt + "/result")
            self.assertEqual(result.status, 200)
            self.assertEqual(result.json()["fragments"][0]["text"], "Rendered page 1")
            self.assertEqual(page.request.get(base + receipt + "/index-status").json()["state"], "Completed")
            browser.close()


if __name__ == "__main__":
    unittest.main()
