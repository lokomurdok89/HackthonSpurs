"""Browser smoke against a disposable API/Postgres, never the developer database."""
import json
import os
from pathlib import Path
import shutil
import socket
import subprocess
import time
import unittest
import urllib.request
import uuid

from playwright.sync_api import expect, sync_playwright
import test_document_api as backend
from test_document_api import CSV, RESULT_EXAMPLES, ROOT, compose, sql


class WorkspaceBrowser(unittest.TestCase):
    authenticate_fixture = True
    profiles = ("--profile", "synthetic")

    @classmethod
    def setUpClass(cls):
        """Start isolated API, Vite and browser processes and register cleanup."""
        cls.addClassCleanup(compose, *cls.profiles, "down", "-v", "--remove-orphans", "--rmi", "local")
        compose(*cls.profiles, "up", "--build", "-d", "--wait")
        backend.DocumentsAPI.ready(authenticate=cls.authenticate_fixture)
        api_port = backend.DocumentsAPI.base.rsplit(":", 1)[1]
        with socket.socket() as sock:
            sock.bind(("127.0.0.1", 0))
            port = sock.getsockname()[1]
        web = ROOT / "frontend"
        vite = subprocess.Popen(
            [shutil.which("node"), str(Path("node_modules/vite/bin/vite.js")), "--mode", "api",
             "--port", str(port), "--strictPort"],
            cwd=web, env=dict(os.environ, API_PORT=api_port, VITE_API_MODE="http"),
        )
        def stop_vite():
            """Terminate the Vite process owned by this test and wait for its exit."""
            vite.terminate()
            vite.wait(timeout=10)
        cls.addClassCleanup(stop_vite)
        cls.url = f"http://127.0.0.1:{port}/#/documents"
        for _ in range(60):
            if vite.poll() is not None:
                raise RuntimeError("Vite exited before serving the UI")
            try:
                with urllib.request.urlopen(cls.url.split("#")[0], timeout=2):
                    break
            except OSError:
                time.sleep(.25)
        driver = sync_playwright().start()
        cls.addClassCleanup(driver.stop)
        cls.browser = driver.chromium.launch(channel=os.environ.get("SPURS_BROWSER_CHANNEL"))
        cls.addClassCleanup(cls.browser.close)

    def workspace_page(self):
        """Open an independent browser context authenticated by the real API bootstrap session."""
        page = self.browser.new_page()
        name, value = backend.DocumentsAPI.cookie.split("=", 1)
        page.context.add_cookies([{"name": name, "value": value, "url": self.url.split("#")[0],
                                  "httpOnly": True, "sameSite": "Strict"}])
        self.addCleanup(page.close)
        return page


class DocumentBrowser(WorkspaceBrowser):
    def test_worker_publishes_to_live_screen(self):
        """Verify the fixture Worker publishes the expected cell and the UI retains it on reload."""
        page = self.workspace_page()
        page.goto(self.url)
        page.get_by_label("Select a document").set_input_files(
            str(ROOT / "backend/HackthonSpurs.Reader.Fixture/worker-demo.csv"))
        page.get_by_role("button", name="Upload document").click()
        expect(page.get_by_role("status")).to_have_text("Completed · Attempt 1", timeout=20000)
        expect(page.get_by_role("heading", name="Reading · synthetic-fixture/1")).to_be_visible()
        expect(page.get_by_text("Row 2, column 1")).to_be_visible()
        expect(page.get_by_text("DEMO-ONLY-014", exact=False)).to_be_visible()
        page.reload()
        expect(page.get_by_role("heading", name="Reading · synthetic-fixture/1")).to_be_visible()

    def test_mock_replay_uses_bytes_not_name_and_size(self):
        """Verify the mock detects changed bytes even with identical filenames and sizes."""
        page = self.workspace_page()
        page.goto(self.url)
        observed = page.evaluate("""async () => {
            const { mockApi } = await import('/src/api/mock.ts');
            const makeFile = (text, name) => new File([text], name, { type: 'text/csv' });
            const key = crypto.randomUUID();
            const first = await mockApi.upload(makeFile('a,b', 'sample.csv'), key);
            const replay = await mockApi.upload(makeFile('a,b', 'renamed.csv'), key);
            try { await mockApi.upload(makeFile('a,c', 'sample.csv'), key); }
            catch (error) { return [first.id === replay.id, error.problem.code]; }
        }""")
        self.assertEqual(observed, [True, "idempotency_conflict"])

    def test_live_upload_replay_resume_and_warning(self):
        """Lose the first receipt, replay it, reload and verify a located reading warning."""
        page = self.workspace_page()
        page.goto(self.url)
        expect(page.get_by_text("Local API ·", exact=False)).to_be_visible()
        upload = page.get_by_role("button", name="Upload document")
        file = page.get_by_label("Select a document")
        file.set_input_files({"name": "empty.csv", "mimeType": "text/csv", "buffer": b""})
        upload.click()
        expect(page.get_by_role("alert")).to_contain_text("non-empty")

        accepted = {}
        def lose_receipt(route):
            """Forward the intercepted route, retain its receipt and simulate a lost client response."""
            response = route.fetch()
            self.assertEqual(response.status, 202)
            accepted.update(response.json())
            accepted["key"] = route.request.headers["idempotency-key"]
            route.abort()
        page.route("**/api/documents", lose_receipt, times=1)
        file.set_input_files({"name": "sample.csv", "mimeType": "text/csv", "buffer": CSV})
        upload.click()
        expect(page.get_by_role("alert")).to_contain_text("Retry this submission")
        upload.click()
        expect(page.get_by_role("status")).to_have_text("Queued · Attempt 0")
        doc = str(uuid.UUID(accepted["id"]))
        self.assertTrue(page.url.endswith("/documents/" + doc))
        key = str(uuid.UUID(accepted["key"]))
        self.assertEqual(sql(f"""SELECT "Id" FROM document_jobs WHERE "OperationKey"='{key}';"""), doc)
        page.reload()
        expect(page.get_by_role("status")).to_have_text("Queued · Attempt 0")

        # Private fixture stands in for future Worker publication, not a real reading.
        result = dict(RESULT_EXAMPLES["unreadableSection"]["value"], documentId=doc)
        sql(f"""UPDATE document_jobs SET "State"='Completed', "Attempt"=1,
            "ResultJson"=$fixture${json.dumps(result)}$fixture$ WHERE "Id"='{doc}';""")
        expect(page.get_by_role("heading", name="Reading warnings")).to_be_visible(timeout=10000)
        expect(page.get_by_text(result["warnings"][0]["message"], exact=False)).to_be_visible()
        page.goto(self.url + "/" + str(uuid.uuid4()))
        expect(page.get_by_role("status")).to_contain_text("unknown")


if __name__ == "__main__":
    unittest.main(verbosity=2)
