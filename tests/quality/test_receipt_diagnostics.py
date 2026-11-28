"""Exercise receipt diagnosis against a disposable loopback mock, without Docker or SPUR."""

from http.server import BaseHTTPRequestHandler, HTTPServer
import json
from pathlib import Path
import subprocess
import sys
from threading import Thread
import unittest


ROOT = Path(__file__).resolve().parents[2]
TOOL = ROOT / "tools/diagnose_receipt.py"
RECEIPT = "10000000-0000-4000-8000-000000000002"
READING = f"/api/documents/{RECEIPT}"
INDEX = READING + "/index-status"
PRIVATE = "synthetic-private-filename.pdf -- invented text and credential"


def status(state, attempt, **fields):
    """Return the synthetic receipt state and attempt with optional mock response fields."""
    return {"id": RECEIPT, "state": state, "attempt": attempt, **fields}


class ReceiptDiagnostics(unittest.TestCase):
    def run_tool(self, replies, expected_exit=0, *arguments, with_index=True):
        """Run the CLI using fake route replies and verify the exit code and requests with or without the optional index lane."""
        requests = []

        class StatusHandler(BaseHTTPRequestHandler):
            def log_message(self, _format, *_args):
                """Suppress standard HTTP logs, regardless of format and arguments."""

            def do_GET(self):
                """Record the loopback lookup and respond with the supplied synthetic fixture."""
                requests.append((self.command, self.path, self.headers.get("Authorization"),
                                 self.headers.get("Content-Length", "0")))
                status_code, payload = replies.get(self.path, (404, PRIVATE))
                data = json.dumps(payload).encode("utf-8") if not isinstance(payload, bytes) else payload
                self.send_response(status_code)
                self.send_header("Content-Type", "application/json")
                self.send_header("Content-Length", str(len(data)))
                if status_code == 302:
                    self.send_header("Location", "/must-not-follow")
                self.end_headers()
                self.wfile.write(data)

        with HTTPServer(("127.0.0.1", 0), StatusHandler) as server:
            thread = Thread(target=server.serve_forever, kwargs={"poll_interval": 0.01}, daemon=True)
            thread.start()
            try:
                process = subprocess.run(
                    [sys.executable, "-B", str(TOOL), (arguments or (RECEIPT,))[0],
                     "--port", str(server.server_port), *arguments[1:], *(["--with-index"] if with_index else [])],
                    cwd=ROOT, capture_output=True, text=True, timeout=10,
                )
            finally:
                server.shutdown()
                thread.join()
        self.assertEqual(process.returncode, expected_exit, process.stderr)
        self.assertNotIn(PRIVATE, process.stdout + process.stderr)
        self.assertNotIn(RECEIPT, process.stdout + process.stderr)
        if expected_exit == 2:
            self.assertEqual(requests, [])
            return None
        paths = [READING, INDEX] if with_index else [READING]
        self.assertEqual(requests, [("GET", path, None, "0") for path in paths])
        self.assertEqual(process.stderr, "")
        return json.loads(process.stdout)

    def test_render_failure_does_not_claim_gateway_was_not_called(self):
        """Keep broad render/scratch errors scoped to the recorded code, not a specific renderer cause."""
        report = self.run_tool({
            READING: (200, status("Failed", 1, failureCode="document_not_renderable", text=PRIVATE)),
            INDEX: (200, status("NotApplicable", 0, chunkCount=0, sourceName=PRIVATE)),
        })
        self.assertEqual(report, {
            "reading": {"state": "Failed", "attempt": 1, "failureCode": "document_not_renderable",
                        "failureStage": "render_or_scratch_io"},
            "index": {"state": "NotApplicable", "attempt": 0},
        })

    def test_preflight_and_gateway_are_distinct(self):
        """Resolve verified preflight and gateway causes from their exact safe codes."""
        for code, stage in (("too_many_pages", "render_preflight"), ("reader_request_rejected", "ocr_gateway")):
            with self.subTest(code=code):
                report = self.run_tool({
                    READING: (200, status("Failed", 1, failureCode=code)),
                    INDEX: (200, status("NotApplicable", 0, chunkCount=0)),
                })
                self.assertEqual(report["reading"]["failureStage"], stage)

    def test_ambiguous_and_untrusted_codes_do_not_invent_a_stage(self):
        """Retain only known ambiguous codes and leave unknown or cross-lane values unreported."""
        for code, exposed in (("reader_rejected", "reader_rejected"), ("document_timeout", "document_timeout"),
                              ("lease_expired", "lease_expired"), (PRIVATE, None), (None, None),
                              ("embedding_response_invalid", None)):
            with self.subTest(code=code):
                report = self.run_tool({
                    READING: (200, status("Failed", 1, **({"failureCode": code} if code is not None else {}))),
                    INDEX: (200, status("NotApplicable", 0, chunkCount=0)),
                })
                self.assertEqual(report["reading"]["failureCode"], exposed)
                self.assertEqual(report["reading"]["failureStage"], "unknown")

    def test_index_failure_does_not_reclassify_completed_reading(self):
        """Show a separate embedding error without exposing chunk counts, transcript or result metadata."""
        report = self.run_tool({
            READING: (200, status("Completed", 1, failureCode=PRIVATE, result={"text": PRIVATE})),
            INDEX: (200, status("Failed", 3, chunkCount=0, failureCode="embedding_response_invalid")),
        })
        self.assertEqual(report, {
            "reading": {"state": "Completed", "attempt": 1},
            "index": {"state": "Failed", "attempt": 3, "failureCode": "embedding_response_invalid",
                      "failureStage": "index_embedding"},
        })

    def test_pending_statuses_are_observations_not_failures(self):
        """Pass through pending snapshots without deriving a failure or requiring an atomic read."""
        report = self.run_tool({
            READING: (200, status("Queued", 0)),
            INDEX: (200, status("Processing", 1, chunkCount=0)),
        })
        self.assertEqual(report, {"reading": {"state": "Queued", "attempt": 0},
                                  "index": {"state": "Processing", "attempt": 1}})

    def test_http_errors_and_redirects_are_not_job_failures(self):
        """Report numeric HTTP statuses only, and never follow a redirect."""
        report = self.run_tool({READING: (404, {"detail": PRIVATE}), INDEX: (302, PRIVATE)}, 1)
        self.assertEqual(report, {"reading": {"error": "http_status", "httpStatus": 404},
                                  "index": {"error": "http_status", "httpStatus": 302}})

    def test_invalid_status_does_not_hide_other_lane(self):
        """Reject unusable responses and still show independently verified indexing status."""
        for bad in (status("Failed", True), status(PRIVATE, 1), status("Failed", 1, id="wrong"),
                    PRIVATE.encode(), b"[" * 1500 + b"]" * 1500, b"x" * 9000):
            with self.subTest(kind=type(bad).__name__):
                report = self.run_tool({READING: (200, bad), INDEX: (200, status("Skipped", 0, chunkCount=0))}, 1)
                self.assertEqual(report, {"reading": {"error": "invalid_status_response"},
                                          "index": {"state": "Skipped", "attempt": 0}})

    def test_bad_arguments_never_contact_api(self):
        """Reject a non-UUID or unexpected argument without reflecting it to output."""
        self.run_tool({}, 2, PRIVATE)

    def test_default_respects_published_api(self):
        """Check a reading without requesting the optional index route on a baseline API."""
        report = self.run_tool({READING: (200, status("Completed", 1))}, with_index=False)
        self.assertEqual(report, {"reading": {"state": "Completed", "attempt": 1}})

    def test_mappings_reference_contract_and_catalog(self):
        """Check recognized states against OpenAPI and recognized codes against the resource catalog."""
        sys.path.insert(0, str(TOOL.parent))
        from diagnose_receipt import INDEX_FAILURES, INDEX_STATES, READING_FAILURES, READING_STATES

        spec = json.loads((ROOT / "docs/api/openapi.json").read_text(encoding="utf-8"))
        schemas = spec["components"]["schemas"]
        self.assertEqual(READING_STATES, set(schemas["DocumentStatus"]["properties"]["state"]["enum"]))
        if "/api/documents/{id}/index-status" not in spec["paths"]:
            return  # The baseline contract only defines reading status.
        self.assertEqual(INDEX_STATES, set(schemas["DocumentIndexStatus"]["properties"]["state"]["enum"]))
        catalog_file = ROOT / "backend/HackthonSpurs.Resources/backend-resources.json"
        catalog = json.loads(catalog_file.read_text(encoding="utf-8"))
        self.assertLessEqual(READING_FAILURES.keys() | INDEX_FAILURES.keys(),
                             {error["code"] for error in catalog["Errors"].values()})


if __name__ == "__main__":
    unittest.main()
