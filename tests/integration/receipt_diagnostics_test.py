"""Run the receipt CLI as a reader against a disposable real workspace API and database."""

import json
import secrets
import subprocess
import sys
import unittest
from urllib.parse import urlsplit
import uuid

import test_document_api as fixtures
from document_fixtures import form
from workspace_client import api_json


class AuthenticatedReceiptDiagnostics(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        """Start only the isolated API and database and remove only that project's resources."""
        cls.addClassCleanup(fixtures.compose, "down", "-v", "--remove-orphans", "--rmi", "local")
        fixtures.compose("up", "--build", "-d", "--wait", "api")
        fixtures.DocumentsAPI.ready()
        cls.base = fixtures.DocumentsAPI.base
        cls.admin = fixtures.DocumentsAPI.cookie
        address = urlsplit(cls.base)
        if address.hostname != "127.0.0.1" or not address.port:
            raise AssertionError("Expected a disposable IPv4 loopback API binding")
        cls.port = str(address.port)

    def test_reader_diagnoses_existing_failed_receipt_and_revokes_session(self):
        """Create a private reader and a local synthetic failed receipt; prove login, both GETs and logout."""
        username, password = "receipt-" + uuid.uuid4().hex[:10], secrets.token_urlsafe(32)
        code, _, user = api_json(self.base, "/api/users", {
            "username": username, "displayName": "Synthetic receipt reader", "role": "reader", "password": password,
        }, self.admin)
        self.assertEqual(code, 201)
        self.assertEqual(user["role"], "reader")
        filename = "synthetic-receipt-diagnostic.pdf"
        body, content_type = form([("file", filename, "application/pdf", fixtures.PDF)])
        code, _, receipt = fixtures.DocumentsAPI.http("/api/documents", body, {
            "Content-Type": content_type, "Idempotency-Key": str(uuid.uuid4()),
        })
        self.assertEqual(code, 202)
        receipt_id = str(uuid.UUID(receipt["id"]))
        route = "/api/documents/" + receipt_id
        self.assertEqual(api_json(self.base, route)[0], 401)
        self.assertEqual(api_json(self.base, route + "/index-status")[0], 401)
        fixtures.sql(
            f"""UPDATE document_jobs SET "State"='Failed', "Attempt"=1, "FailureCode"='document_not_renderable'
            WHERE "Id"='{receipt_id}';"""
        )

        user_id = str(uuid.UUID(user["id"]))
        session_count = f"""SELECT count(*) FROM workspace_sessions WHERE "UserId"='{user_id}';"""
        before = fixtures.sql(session_count)
        process = subprocess.run(
            [sys.executable, "-B", str(fixtures.ROOT / "tools/diagnose_receipt.py"), receipt_id,
             "--port", self.port, "--login-stdin", "--with-index"],
            input=json.dumps({"username": username, "password": password}),
            capture_output=True, text=True, timeout=30, cwd=fixtures.ROOT,
        )
        self.assertFalse(any(value in process.stdout + process.stderr for value in
                             (username, password, filename, receipt_id, "origin_vault_session")),
                         "The CLI exposed private input")
        self.assertEqual(process.returncode, 0)
        self.assertFalse(process.stderr, "Unexpected diagnostics on stderr")
        self.assertEqual(json.loads(process.stdout), {
            "reading": {"state": "Failed", "attempt": 1, "failureCode": "document_not_renderable",
                        "failureStage": "render_or_scratch_io"},
            "index": {"state": "NotApplicable", "attempt": 0},
        })
        verified = api_json(self.base, route, cookie=self.admin)[2]
        self.assertEqual((verified["state"], verified["attempt"], verified["failureCode"]),
                         ("Failed", 1, "document_not_renderable"))
        self.assertEqual((before, fixtures.sql(session_count)), ("0", "0"))


if __name__ == "__main__":
    unittest.main(verbosity=2)
