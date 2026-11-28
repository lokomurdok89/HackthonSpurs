"""Minimal black-box auth client for disposable Compose projects; never loads local secrets."""
import json
import secrets
import urllib.error
import urllib.request

ADMIN = "qa-admin"
PASSWORD = secrets.token_urlsafe(32)
REQUEST_HEADER = {"X-Workspace-Request": "1"}


def call(base, path, body=None, headers=None, method=None):
    """Call path at base with optional raw body, headers and verb; return status, headers and decoded JSON."""
    request = urllib.request.Request(base + path, body, headers or {}, method=method)
    try:
        response = urllib.request.urlopen(request, timeout=20)
    except urllib.error.HTTPError as error:
        response = error
    with response:
        payload = response.read()
        return response.status, response.headers, json.loads(payload) if payload else None


def api_json(base, path, data=None, cookie=None, method=None, marked=True):
    """Send data to path at base with an optional session cookie, explicit verb and same-origin marker."""
    headers = {"Content-Type": "application/json", **(REQUEST_HEADER if marked else {})}
    if cookie:
        headers["Cookie"] = cookie
    return call(base, path, json.dumps(data).encode() if data is not None else None, headers, method)


def session(base, username=ADMIN, password=PASSWORD):
    """Sign in at base with ephemeral credentials and return the opaque cookie pair without logging it."""
    code, headers, _ = api_json(base, "/api/auth/login", {"username": username, "password": password})
    if code != 200:
        raise AssertionError(f"Test login returned HTTP {code}")
    return headers["Set-Cookie"].split(";", 1)[0]
