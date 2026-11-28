"""Verify public fixture logins from a clean environment, never against an existing workspace."""
import json
import os
import subprocess
import unittest
import uuid
from pathlib import Path

from workspace_client import api_json, session

ROOT = Path(__file__).resolve().parents[2]


class PublicDemo(unittest.TestCase):
    def test_clean_clone_accounts(self):
        """Build an isolated fixture project, sign in through web, and check that reader rights stay limited."""
        project = "spurs-demo-test-" + uuid.uuid4().hex[:12]
        env = {k: v for k, v in os.environ.items() if not k.upper().startswith(
            ("COMPOSE_", "SPUR_", "AUTH__", "POSTGRES_", "LOCAL_DEMO_"))}
        env.update(COMPOSE_DISABLE_ENV_FILE="1", LOCAL_DEMO_PORT="0")
        cmd = ["docker", "compose", "-p", project, "-f", "compose.demo.yaml"]
        if subnet := env.get("SPURS_TEST_SUBNET"):
            env["SPURS_SUBNET"] = subnet
            cmd.extend(["-f", "compose.subnet.yaml"])

        def compose(*args, capture=False):
            """Run args against only this randomly named project; capture stdout when requested."""
            return subprocess.run([*cmd, *args], cwd=ROOT, env=env, check=True, text=True,
                                  stdout=subprocess.PIPE if capture else None).stdout

        cfg = json.loads(compose("config", "--format", "json", capture=True))
        self.assertEqual(cfg["name"], project)
        self.assertEqual(set(cfg["volumes"]), {"demo-originals", "demo-database"})
        for vol in cfg["volumes"].values():
            self.assertTrue(vol["name"].startswith(project + "_"))
        self.assertEqual(set(cfg["services"]), {"web", "api", "postgres", "worker", "fixture-reader"})
        for name, settings in cfg["services"].items():
            ports = settings.get("ports", [])
            self.assertEqual([p["host_ip"] for p in ports], ["127.0.0.1"] if name == "web" else [])
            self.assertFalse(any("spur" in k.lower() for k in settings.get("environment", {})))

        self.addCleanup(compose, "down", "-v", "--rmi", "local")
        compose("up", "--build", "-d", "--wait", "--wait-timeout", "150")
        url = "http://" + compose("port", "web", "8080", capture=True).strip()
        for username, password, role, list_status in [
                ("demo.admin", "LocalDemo-Admin-2026", "admin", 200),
                ("demo.lector", "LocalDemo-Reader-2026", "reader", 403)]:
            with self.subTest(role=role):
                cookie = session(url, username, password)
                status, _, user = api_json(url, "/api/auth/me", cookie=cookie)
                self.assertEqual((status, user["role"]), (200, role))
                self.assertEqual(api_json(url, "/api/users", cookie=cookie)[0], list_status)


if __name__ == "__main__":
    unittest.main()
