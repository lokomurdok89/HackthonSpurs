"""Exercise demo seeding with real Compose dotenv input, PostgreSQL, API and Chromium; no providers."""
import json
import secrets
import socket
import subprocess
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

from playwright.sync_api import expect
import test_document_api as api
import test_document_ui as ui
from workspace_client import api_json, session

ADMIN, READER = "demo.admin", "demo.lector"


def setup_env(case, enabled=True):
    """Give this test class an isolated dotenv file and explicit API port; restore prior settings at teardown."""
    root = api.ROOT / ".local"
    root.mkdir(exist_ok=True)
    temp = tempfile.TemporaryDirectory(dir=root)
    case.addClassCleanup(temp.cleanup)
    case.dotenv = Path(temp.name) / ".env"
    case.initial = {ADMIN: secrets.token_urlsafe(32), READER: secrets.token_urlsafe(32)}
    write_env(case.dotenv, enabled, case.initial[ADMIN], case.initial[READER])
    file_patch = patch.object(api, "ENV_FILE", str(case.dotenv))
    file_patch.start()
    case.addClassCleanup(file_patch.stop)
    env_patch = patch.dict(api.ENV)
    env_patch.start()
    case.addClassCleanup(env_patch.stop)
    for key in ("Auth__DemoSeedEnabled", "Auth__DemoAdminPassword", "Auth__DemoReaderPassword"):
        api.ENV.pop(key, None)
    with socket.socket() as sock:
        sock.bind(("127.0.0.1", 0))
        api.ENV["API_PORT"] = str(sock.getsockname()[1])


def write_env(path, enabled, admin, reader):
    """Write only disposable private fixture settings to path; never print either supplied password."""
    path.write_text(f"Auth__DemoSeedEnabled={str(enabled).lower()}\nAuth__DemoAdminPassword={admin}\n"
                    f"Auth__DemoReaderPassword={reader}\n", encoding="utf-8")


def sign_in_status(name, password):
    """Return only the HTTP status of an attempted login with name and password."""
    return api_json(api.DocumentsAPI.base, "/api/auth/login", {"username": name, "password": password})[0]


class DemoPasswordRotation(ui.WorkspaceBrowser):
    profiles = ()
    authenticate_fixture = False

    @classmethod
    def setUpClass(cls):
        """Boot with demo mode enabled on an empty database and both passwords supplied by dotenv."""
        setup_env(cls)
        super().setUpClass()

    def test_users_password_survives_restart(self):
        """Change both passwords in Users, retain original dotenv, restart API and verify both identities."""
        base = api.DocumentsAPI.base
        admin_cookie = session(base, ADMIN, self.initial[ADMIN])
        code, _, users = api_json(base, "/api/users", cookie=admin_cookie)
        self.assertEqual(code, 200)
        self.assertEqual({u["username"]: u["role"] for u in users}, {ADMIN: "admin", READER: "reader"})
        self.assertEqual(sign_in_status(READER, self.initial[READER]), 200)
        page = self.browser.new_page()
        self.addCleanup(page.close)
        page.goto(self.url.replace("#/documents", "#/users"))
        signin = page.get_by_role("form", name="Sign in", exact=True)
        signin.get_by_label("Username").fill(ADMIN)
        signin.get_by_label("Password").fill(self.initial[ADMIN])
        signin.get_by_role("button", name="Sign in").click()
        updated = {}
        for name in (READER, ADMIN):
            row = page.get_by_role("row").filter(has_text=name)
            row.get_by_role("button", name="Edit").click()
            edit = page.get_by_role("form", name="Edit user")
            updated[name] = secrets.token_urlsafe(32)
            edit.get_by_label("New password").fill(updated[name])
            with page.expect_response(lambda r: r.request.method == "PATCH" and "/api/users/" in r.url) as saved:
                edit.get_by_role("button", name="Save changes").click()
            self.assertEqual(saved.value.status, 200)
            expect(edit).not_to_be_visible()
        expect(signin).to_be_visible()
        api.compose("restart", "api")
        api.DocumentsAPI.ready(authenticate=False)
        for name in (ADMIN, READER):
            self.assertEqual(sign_in_status(name, self.initial[name]), 401)
            self.assertEqual(sign_in_status(name, updated[name]), 200)
        fresh = session(base, ADMIN, updated[ADMIN])
        self.assertEqual(api_json(base, "/api/users", cookie=fresh)[2], users)


class DemoSeedBoundaries(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        """Start only a private database and build the API for isolated startup checks."""
        setup_env(cls, enabled=False)
        cls.addClassCleanup(api.compose, "down", "-v", "--remove-orphans", "--rmi", "local")
        api.compose("up", "-d", "--build", "--wait", "postgres")
        api.compose("build", "api")

    def rejected(self, message_key):
        """Assert a one-off API stops with the specific catalog message_key, without printing its logs."""
        cmd = ["docker", "compose", "--env-file", api.ENV_FILE, "-p", api.PROJECT,
               "run", "--rm", "--no-deps", "-T", "api"]
        result = subprocess.run(cmd, cwd=api.ROOT, env=api.ENV, capture_output=True, timeout=30)
        self.assertNotEqual(result.returncode, 0)
        catalog = api.ROOT / "backend/HackthonSpurs.Resources/backend-resources.json"
        expected = json.loads(catalog.read_text(encoding="utf-8"))["ResourceText"][message_key].encode()
        self.assertTrue(expected in result.stdout + result.stderr, "API stopped without the expected seed error")

    def test_missing_only_and_conflicts(self):
        """Verify all-or-nothing, opt-out, case-insensitive collision and preserving an existing disabled reader."""
        write_env(self.dotenv, True, self.initial[ADMIN], "")
        self.rejected("DemoSeedPasswordInvalid")
        self.assertEqual(api.sql("SELECT count(*) FROM workspace_users"), "0")
        write_env(self.dotenv, True, self.initial[ADMIN], self.initial[ADMIN])
        self.rejected("DemoSeedPasswordInvalid")
        self.assertEqual(api.sql("SELECT count(*) FROM workspace_users"), "0")

        write_env(self.dotenv, False, "", "")
        api.compose("up", "-d", "--no-deps", "api")
        api.DocumentsAPI.ready()
        base, cookie = api.DocumentsAPI.base, api.DocumentsAPI.cookie
        self.assertEqual(api.sql("SELECT count(*) FROM workspace_users"), "1")
        create = {"username": READER.upper(), "displayName": "Keep me", "password": self.initial[READER], "role": "admin"}
        code, _, prior = api_json(base, "/api/users", create, cookie)
        self.assertEqual(code, 201)
        write_env(self.dotenv, True, self.initial[ADMIN], "")
        self.rejected("DemoSeedRoleConflict")
        self.assertEqual(api.sql("SELECT count(*) FROM workspace_users"), "2")
        code, _, prior = api_json(base, "/api/users/" + prior["id"], {"role": "reader", "active": False}, cookie, "PATCH")
        self.assertEqual(code, 200)
        api.compose("up", "-d", "--no-deps", "--force-recreate", "api")
        api.DocumentsAPI.ready(authenticate=False)
        fresh = session(base, ADMIN, self.initial[ADMIN])
        users = api_json(base, "/api/users", cookie=fresh)[2]
        self.assertEqual(len(users), 3)
        self.assertIn(prior, users)
        self.assertEqual(sign_in_status(READER, self.initial[READER]), 401)


if __name__ == "__main__":
    unittest.main(verbosity=2)
