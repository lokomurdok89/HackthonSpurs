"""Verify that the receipt CLI honors authentication and wire-contract boundaries."""

import json
import sys
import unittest

from test_receipt_diagnostics import LoopbackReceiptFixture, LOGOUT, PRIVATE, READING, ROOT, SET_COOKIE, TOOL, status


class ReceiptDiagnosticsAuth(LoopbackReceiptFixture):
    def test_login_failure_does_not_get_status(self):
        """Do not query a receipt after authentication fails or is redirected."""
        for code in (302, 401, 429):
            with self.subTest(code=code):
                report = self.run_tool({}, 1, login_status=code)
                self.assertEqual(report, {"login": {"error": "http_status", "httpStatus": code}})

    def test_untrusted_cookie_does_not_get_status(self):
        """Never forward a missing or out-of-scope authentication cookie."""
        for cookie in (None, SET_COOKIE + "; secure", SET_COOKIE + "; domain=example.invalid",
                       SET_COOKIE.replace("httponly", "")):
            with self.subTest(cookie_presence=cookie is not None):
                report = self.run_tool({}, 1, set_cookie=cookie)
                self.assertEqual(report, {"login": {"error": "invalid_login_response"}})

    def test_interactive_login_requires_terminal(self):
        """Refuse interactive credentials from a nonterminal without echoing the piped input."""
        self.run_tool({}, 2, login_mode="--login")

    def test_logout_failure_does_not_hide_status(self):
        """Keep verified status while reporting a session that could not be revoked."""
        report = self.run_tool({READING: (200, status("Completed", 1)), LOGOUT: (503, PRIVATE)},
                               1, with_index=False)
        self.assertEqual(report, {"reading": {"state": "Completed", "attempt": 1},
                                  "logout": {"error": "http_status", "httpStatus": 503}})

    def test_mappings_reference_contract_and_catalog(self):
        """Check status, login, cookie and failure-code constants against OpenAPI and the resource catalog."""
        sys.path.insert(0, str(TOOL.parent))
        from diagnose_receipt import INDEX_FAILURES, INDEX_STATES, READING_FAILURES, READING_STATES
        from diagnose_receipt_auth import COOKIE_NAME, LOGIN, LOGOUT, MAX_CREDENTIAL_BYTES, REQUEST_MARKER, TOKEN_PATTERN

        spec = json.loads((ROOT / "docs/api/openapi.json").read_text(encoding="utf-8"))
        self.assertIn("post", spec["paths"][LOGIN])
        self.assertIn("post", spec["paths"][LOGOUT])
        schemas = spec["components"]["schemas"]
        self.assertEqual(READING_STATES, set(schemas["DocumentStatus"]["properties"]["state"]["enum"]))
        self.assertEqual(INDEX_STATES, set(schemas["DocumentIndexStatus"]["properties"]["state"]["enum"]))
        catalog_file = ROOT / "backend/HackthonSpurs.Resources/backend-resources.json"
        catalog = json.loads(catalog_file.read_text(encoding="utf-8"))
        wire = catalog["Wire"]
        self.assertEqual(COOKIE_NAME, wire["WorkspaceCookie"])
        self.assertEqual(REQUEST_MARKER, {wire["WorkspaceRequestHeader"]: wire["WorkspaceRequestValue"]})
        token_length = catalog["Policy"]["SessionTokenLength"]
        self.assertIsNotNone(TOKEN_PATTERN.fullmatch("t" * token_length))
        self.assertIsNone(TOKEN_PATTERN.fullmatch("t" * (token_length + 1)))
        self.assertEqual(MAX_CREDENTIAL_BYTES, catalog["Policy"]["MaxAuthBody"])
        self.assertLessEqual(READING_FAILURES.keys() | INDEX_FAILURES.keys(),
                             {error["code"] for error in catalog["Errors"].values()})


if __name__ == "__main__":
    unittest.main()
