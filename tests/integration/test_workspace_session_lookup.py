"""Verify the session read projection and account writer together on an isolated local PostgreSQL workspace."""
import secrets
import unittest
import uuid

import test_document_api as fixtures
from workspace_client import api_json, session


class WorkspaceSessionLookup(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        """Provision a disposable database with its own synthetic credentials and login rate budget."""
        cls.addClassCleanup(fixtures.compose, "down", "-v", "--remove-orphans", "--rmi", "local")
        fixtures.compose("up", "--build", "-d", "--wait")
        fixtures.DocumentsAPI.ready()

    def test_profile_reads_and_password_revocation(self):
        """Two independent sessions see a display edit without session writes; a password reset revokes both."""
        base, admin = fixtures.DocumentsAPI.base, fixtures.DocumentsAPI.cookie
        username, password = "lookup-" + uuid.uuid4().hex[:10], secrets.token_urlsafe(32)
        code, _, user = api_json(base, "/api/users", {"username": username, "password": password,
                                 "role": "reader", "displayName": "Before rename"}, admin)
        self.assertEqual(code, 201)
        user_id = uuid.UUID(user["id"])
        route = "/api/users/" + str(user_id)
        first, second = session(base, username, password), session(base, username, password)
        self.assertTrue(first != second, "Expected distinct login sessions")
        # Collect only PostgreSQL row versions, never a cookie, session digest or password hash.
        versions = f"""SELECT xmin::text FROM workspace_sessions WHERE "UserId"='{user_id}' ORDER BY xmin::text"""
        before = fixtures.sql(versions).splitlines()
        self.assertEqual(len(before), 2)
        self.assertEqual(api_json(base, route, {"displayName": "Renamed reader"}, admin, "PATCH")[0], 200)
        for cookie in (first, second):
            status, _, current = api_json(base, "/api/auth/me", cookie=cookie)
            self.assertEqual(status, 200)
            self.assertEqual((current["displayName"], current["role"]), ("Renamed reader", "reader"))
        self.assertEqual(fixtures.sql(versions).splitlines(), before)
        new_password = secrets.token_urlsafe(32)
        self.assertEqual(api_json(base, route, {"password": new_password}, admin, "PATCH")[0], 200)
        self.assertEqual(fixtures.sql(versions).splitlines(), [])
        for cookie in (first, second):
            self.assertEqual(api_json(base, "/api/auth/me", cookie=cookie)[0], 401)
        self.assertEqual(api_json(base, "/api/auth/login", {"username": username, "password": password})[0], 401)
        fresh = session(base, username, new_password)
        self.assertEqual(api_json(base, "/api/users", cookie=fresh)[0], 403)
        self.assertEqual(api_json(base, "/api/auth/me", cookie=admin)[0], 200)


if __name__ == "__main__":
    unittest.main(verbosity=2)
