"""Demonstrate login, user maintenance and the separate buyer passport using a real Chromium context."""
import secrets
import unittest
import uuid
from urllib.parse import urlsplit

from playwright.sync_api import expect
import test_document_ui as fixture
from workspace_client import ADMIN, PASSWORD, api_json


class WorkspaceScreens(fixture.WorkspaceBrowser):
    profiles = ()

    def login(self, page, username, password):
        """Sign in through the visible form on page using the named test account and its ephemeral password."""
        # Logout is asynchronous: never fill the still-mounted admin account form.
        form = page.get_by_role("form", name="Sign in", exact=True)
        form.get_by_label("Username", exact=True).fill(username)
        form.get_by_label("Password", exact=True).fill(password)
        with page.expect_response(lambda r: r.request.method == "POST" and r.url.endswith("/api/auth/login")) as login:
            form.get_by_role("button", name="Sign in", exact=True).click()
        self.assertEqual(login.value.status, 200, "Expected a successful workspace login")

    def test_maintainer_and_reader(self):
        """Create and edit an account, switch identity and confirm reader-only controls survive direct navigation."""
        page = self.browser.new_page()
        self.addCleanup(page.close)
        users_url = self.url.replace("#/documents", "#/users")
        page.goto(users_url)
        expect(page.get_by_label("Username", exact=True)).to_be_visible()
        expect(page.get_by_role("button", name="Create user", exact=True)).to_have_count(0)
        self.login(page, ADMIN, PASSWORD)
        form = page.get_by_role("form", name="Create user", exact=True)
        name, password = "browser-" + uuid.uuid4().hex[:8], secrets.token_urlsafe(32)
        form.get_by_label("Username", exact=True).fill(name)
        form.get_by_label("Display name", exact=True).fill("Reader demo")
        form.get_by_label("Password", exact=True).fill(password)
        form.get_by_label("Role", exact=True).select_option("reader")
        form.get_by_role("button", name="Create user").click()
        row = page.get_by_role("row").filter(has_text=name)
        row.get_by_role("button", name="Edit").click()
        edit = page.get_by_role("form", name="Edit user", exact=True)
        edit.get_by_label("Display name", exact=True).fill("Updated reader")
        edit.get_by_role("button", name="Save changes").click()
        expect(row).to_contain_text("Updated reader")
        page.reload()
        expect(row).to_contain_text("Updated reader")
        page.get_by_role("button", name="Sign out").click()
        self.login(page, name, password)
        expect(page.get_by_role("button", name="Sign out")).to_be_visible()
        expect(page.get_by_role("link", name="Users", exact=True)).to_have_count(0)
        page.goto(users_url)
        expect(page.get_by_role("form", name="Create user")).to_have_count(0)
        page.goto(self.url)
        expect(page.get_by_role("heading", name="Documents", exact=True)).to_be_visible()
        expect(page.locator("input[type=file]")).to_have_count(0)
        page.goto(self.url.replace("#/documents", "#/review"))
        expect(page.get_by_role("button", name="Approve", exact=True)).to_have_count(0)
        # A separate administrator resets this reader's password while its browser is still signed in.
        base = fixture.backend.DocumentsAPI.base
        admin = fixture.backend.DocumentsAPI.cookie
        users = api_json(base, "/api/users", cookie=admin)[2]
        uid = next(user["id"] for user in users if user["username"] == name)
        self.assertEqual(api_json(base, "/api/users/" + uid, {"password": secrets.token_urlsafe(32)}, admin, "PATCH")[0], 200)
        page.reload()
        expect(page.get_by_label("Username", exact=True)).to_be_visible()

    def test_public_passport_does_not_call_private_endpoints(self):
        """Open the synthetic passport in a fresh, unsigned browser and observe only public sample data."""
        page = self.browser.new_page()
        self.addCleanup(page.close)
        private = []
        page.on("request", lambda request: private.append(request.method) if urlsplit(request.url).path.startswith("/api/") else None)
        page.emulate_media(color_scheme="dark")
        page.goto(self.url.replace("#/documents", "#/p/EX-100?share=0&lots=0"))
        expect(page.locator(".theme-light").first).to_have_css("color-scheme", "light")
        expect(page.get_by_text("Example welded bracket", exact=False).first).to_be_visible()
        expect(page.get_by_label("Username", exact=True)).to_have_count(0)
        self.assertEqual(private, [])


if __name__ == "__main__":
    unittest.main(verbosity=2)
