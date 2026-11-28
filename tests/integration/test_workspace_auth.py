"""Exercise real workspace sessions, role enforcement and account changes against an isolated API."""
import concurrent.futures
import hashlib
import secrets
import unittest
import uuid
from unittest.mock import patch

import test_document_api as fixtures
from workspace_client import ADMIN, PASSWORD, api_json, session

PUBLIC_FIELDS = {"id", "username", "displayName", "role", "active"}


class WorkspaceAuth(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        """Start a disposable database and API with a random bootstrap secret."""
        cls.addClassCleanup(fixtures.compose, "down", "-v", "--remove-orphans", "--rmi", "local")
        fixtures.compose("up", "--build", "-d", "--wait")
        fixtures.DocumentsAPI.ready()
        cls.base = fixtures.DocumentsAPI.base
        cls.admin = fixtures.DocumentsAPI.cookie

    def create(self, role="reader"):
        """Create an enabled synthetic account with the requested role and return its DTO and generated password."""
        password = secrets.token_urlsafe(32)
        name = "qa-" + uuid.uuid4().hex[:10]
        code, _, user = api_json(self.base, "/api/users", {"username": name, "displayName": "Test account",
                                 "role": role, "password": password}, self.admin)
        self.assertEqual(code, 201)
        self.assertEqual(set(user), PUBLIC_FIELDS)
        self.assertEqual((user["role"], user["active"]), (role, True))
        return user, password

    def change(self, user, fields, actor=None):
        """Apply fields to user through the maintenance endpoint as actor or the bootstrap administrator."""
        return api_json(self.base, "/api/users/" + user["id"], fields, actor or self.admin, "PATCH")

    def test_access_matrix(self):
        """Protect every existing document/search route and user action, including direct URL access."""
        reader, password = self.create()
        reader_cookie = session(self.base, reader["username"].upper(), password)
        doc = "/api/documents/" + str(uuid.uuid4())
        for path, data, verb in [(doc, None, "GET"), (doc + "/result", None, "GET"),
                                 (doc + "/index-status", None, "GET"), ("/api/search/stats", None, "GET"),
                                 ("/api/search", {"query": "test"}, "POST"), ("/api/documents", {}, "POST"),
                                 ("/api/users", None, "GET"), ("/api/users", {}, "POST"),
                                 ("/api/users/" + reader["id"], {"role": "admin"}, "PATCH")]:
            with self.subTest(path=path, verb=verb):
                self.assertEqual(api_json(self.base, path, data, method=verb)[0], 401)
                code = api_json(self.base, path, data, reader_cookie, verb)[0]
                if path.startswith("/api/users") or path == "/api/documents":
                    self.assertEqual(code, 403)
                elif path.startswith(doc):
                    self.assertEqual(code, 404)
                else:
                    self.assertIn(code, [200, 503])
        self.assertEqual(api_json(self.base, "/api/auth/me", cookie=reader_cookie)[2], reader)
        users = api_json(self.base, "/api/users", cookie=self.admin)[2]
        self.assertTrue(all(set(user) == PUBLIC_FIELDS for user in users))

    def test_account_lifecycle(self):
        """Ensure role changes, password reset, deactivation and logout invalidate already-issued cookies."""
        user, password = self.create()
        def sign_in():
            """Open an independent session with the current synthetic user's password."""
            return session(self.base, user["username"], password)
        def revoked(cookie):
            """Assert that cookie cannot resolve even the caller's session after revocation."""
            self.assertEqual(api_json(self.base, "/api/auth/me", cookie=cookie)[0], 401)
        old = sign_in()
        code, _, changed = self.change(user, {"role": "admin", "displayName": "Changed name"})
        self.assertEqual((code, changed["role"], changed["displayName"]), (200, "admin", "Changed name"))
        revoked(old)
        old = sign_in()
        previous, password = password, secrets.token_urlsafe(32)
        self.assertEqual(self.change(user, {"password": password})[0], 200)
        revoked(old)
        self.assertEqual(api_json(self.base, "/api/auth/login", {"username": user["username"], "password": previous})[0], 401)
        old = sign_in()
        self.assertEqual(self.change(user, {"active": False})[0], 200)
        revoked(old)
        self.assertEqual(api_json(self.base, "/api/auth/login", {"username": user["username"], "password": password})[0], 401)
        self.assertEqual(self.change(user, {"active": True, "role": "reader"})[0], 200)
        current = sign_in()
        self.assertEqual(api_json(self.base, "/api/auth/logout", cookie=current, method="POST")[0], 204)
        revoked(current)

    def test_cookie_and_input_boundaries(self):
        """Check cookie flags, generic login failures, duplicate normalized names, and invalid changes."""
        code, headers, user = api_json(self.base, "/api/auth/login", {"username": ADMIN, "password": PASSWORD})
        self.assertEqual(code, 200)
        self.assertEqual(set(user), PUBLIC_FIELDS)
        flags = headers["Set-Cookie"].lower()
        self.assertTrue("httponly" in flags and "samesite=strict" in flags)
        self.assertIn("no-store", headers["Cache-Control"])
        pseudo = {"username": ADMIN.upper(), "password": PASSWORD, "role": "admin", "displayName": "Duplicate"}
        self.assertEqual(api_json(self.base, "/api/users", pseudo, self.admin)[0], 409)
        self.assertEqual(self.change(user, {"role": "reader"})[0], 409)
        self.assertEqual(self.change(user, {"active": False})[0], 409)
        for fields in ({"role": "owner"}, {"displayName": " "}, {"password": "short"}):
            self.assertEqual(self.change(user, fields)[0], 400)
        def failure(name):
            """Submit a random invalid password for name and project the public failure."""
            status, _, problem = api_json(self.base, "/api/auth/login", {"username": name, "password": secrets.token_hex(12)})
            return status, problem["code"], problem["title"]
        self.assertEqual(failure(ADMIN), failure("missing-test-user"))

    def test_request_marker(self):
        """Reject browser writes without the same-origin marker before creating sessions or modifying data."""
        for path, data, cookie in [
                ("/api/auth/login", {"username": ADMIN, "password": PASSWORD}, None),
                ("/api/auth/logout", None, self.admin), ("/api/documents", {}, self.admin),
                ("/api/search", {"query": "synthetic"}, self.admin), ("/api/users", {}, self.admin)]:
            self.assertEqual(api_json(self.base, path, data, cookie, "POST", marked=False)[0], 403)
        self.assertEqual(api_json(self.base, "/api/auth/me", cookie=self.admin)[0], 200)

    def test_session_storage_and_expiry(self):
        """Check that the DB retains hashes, not browser secrets, and that expiry is enforced server side."""
        raw = self.admin.split("=", 1)[1]
        digest = hashlib.sha256(raw.encode("ascii")).hexdigest().upper()
        self.assertEqual(fixtures.sql(f"""SELECT count(*) FROM workspace_sessions WHERE "Digest"='{digest}'"""), "1")
        self.assertTrue(fixtures.sql("""SELECT length("PasswordHash") > 0 FROM workspace_users
                                     WHERE "Username"='qa-admin'""") == "t")
        # Expire only a new synthetic account's sessions; leave the bootstrap administrator online.
        user, password = self.create()
        cookie = session(self.base, user["username"], password)
        uid = str(uuid.UUID(user["id"]))
        fixtures.sql(f"""UPDATE workspace_sessions SET "ExpiresAt"=now()-interval '1 second' WHERE "UserId"='{uid}'""")
        self.assertEqual(api_json(self.base, "/api/auth/me", cookie=cookie)[0], 401)

    def test_y_restart_and_concurrent_demotion(self):
        """Changing bootstrap inputs does not rotate credentials; two admins cannot demote each other to zero."""
        original = fixtures.ENV["Auth__BootstrapPassword"]
        try:
            fixtures.ENV["Auth__BootstrapPassword"] = secrets.token_urlsafe(32)
            fixtures.compose("up", "-d", "--no-deps", "--force-recreate", "api")
            fixtures.DocumentsAPI.ready()  # Signs in with the original password on the persistent volume.
        finally:
            fixtures.ENV["Auth__BootstrapPassword"] = original
        type(self).base = fixtures.DocumentsAPI.base
        bootstrap = api_json(self.base, "/api/auth/me", cookie=self.admin)[2]
        self.assertEqual(bootstrap["role"], "admin")
        other, password = self.create("admin")
        other_cookie = session(self.base, other["username"], password)
        with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
            results = [pool.submit(self.change, other, {"role": "reader"}, self.admin),
                       pool.submit(self.change, bootstrap, {"role": "reader"}, other_cookie)]
            codes = sorted(result.result()[0] for result in results)
        self.assertEqual(codes[0], 200)
        self.assertIn(codes[1], (401, 403, 409))
        self.assertEqual(fixtures.sql("""SELECT count(*) FROM workspace_users WHERE "Active" AND "Role"='admin'"""), "1")

    def test_z_rate_limit(self):
        """Exercise the bounded login budget at the end of this disposable single-origin suite."""
        for _ in range(16):
            code = api_json(self.base, "/api/auth/login", {"username": "missing-account", "password": "not-correct"})[0]
            if code == 429:
                break
            self.assertEqual(code, 401)
        self.assertEqual(code, 429)


class BootstrapBoundary(unittest.TestCase):
    def test_no_default_or_registration(self):
        """On a new DB without bootstrap inputs the workspace is locked, not implicitly public or claimable."""
        self.addCleanup(fixtures.compose, "down", "-v", "--remove-orphans", "--rmi", "local")
        with patch.dict(fixtures.ENV, {"Auth__BootstrapUsername": "", "Auth__BootstrapPassword": ""}):
            fixtures.compose("up", "--build", "-d", "--wait")
            fixtures.DocumentsAPI.ready(authenticate=False)
            base = fixtures.DocumentsAPI.base
            self.assertEqual(fixtures.sql("SELECT count(*) FROM workspace_users"), "0")
            self.assertEqual(api_json(base, "/api/auth/login", {"username": ADMIN, "password": PASSWORD})[0], 401)
            self.assertEqual(api_json(base, "/api/users", {"username": ADMIN, "password": PASSWORD,
                                     "role": "admin", "displayName": "No self-registration"})[0], 401)
            self.assertEqual(fixtures.sql("SELECT count(*) FROM workspace_users"), "0")


if __name__ == "__main__":
    unittest.main(verbosity=2)
