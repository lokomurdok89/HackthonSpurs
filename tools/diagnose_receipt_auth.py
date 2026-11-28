"""Open a temporary authenticated workspace session for a loopback diagnostic."""

from contextlib import contextmanager
from getpass import GetPassWarning, getpass
from http.client import HTTPConnection, HTTPException
from http.cookies import CookieError, SimpleCookie
import json
import re
import sys
import warnings


LOGIN = "/api/auth/login"
LOGOUT = "/api/auth/logout"
REQUEST_MARKER = {"X-Workspace-Request": "1"}
COOKIE_NAME = "origin_vault_session"
TOKEN_PATTERN = re.compile(r"[A-Za-z0-9_-]{43}")
MAX_CREDENTIAL_BYTES = 4096
REQUEST_TIMEOUT_SECONDS = 5


@contextmanager
def local_response(port, method, path, headers, body=None):
    """Send method to path on the loopback port with the supplied headers and body."""
    connection = HTTPConnection("127.0.0.1", port, timeout=REQUEST_TIMEOUT_SECONDS)
    try:
        connection.request(method, path, headers=headers, body=body)
        with connection.getresponse() as response:
            yield response
    finally:
        connection.close()


def read_credentials(from_stdin):
    """Read one bounded JSON input from a private pipe or use prompts that never fall back to echo."""
    try:
        if from_stdin:
            if sys.stdin.isatty():
                return None
            raw = sys.stdin.buffer.read(MAX_CREDENTIAL_BYTES + 1)
            values = json.loads(raw) if len(raw) <= MAX_CREDENTIAL_BYTES else None
        else:
            if not sys.stdin.isatty():
                return None
            with warnings.catch_warnings():
                warnings.simplefilter("error", GetPassWarning)
                values = {"username": getpass("Workspace username: "), "password": getpass("Workspace password: ")}
        if (not isinstance(values, dict) or values.keys() != {"username", "password"}
                or not all(isinstance(value, str) and value for value in values.values())):
            return None
        body = json.dumps(values).encode("ascii")
        return body if len(body) <= MAX_CREDENTIAL_BYTES else None
    except (EOFError, OSError, ValueError, GetPassWarning, KeyboardInterrupt, RecursionError):
        return None


def workspace_cookie(headers):
    """Extract one valid, host-only workspace cookie from the supplied login response headers."""
    found = []
    for header in headers.get_all("Set-Cookie", []):
        cookies = SimpleCookie()
        cookies.load(header)
        if COOKIE_NAME in cookies:
            found.append(cookies[COOKIE_NAME])
    if len(found) != 1:
        return None
    cookie = found[0]
    if (not TOKEN_PATTERN.fullmatch(cookie.value) or cookie["domain"] or cookie["secure"]
            or cookie["path"] != "/" or not cookie["httponly"]):
        return None
    return f"{COOKIE_NAME}={cookie.value}"


def sign_in(port, credentials):
    """Use the supplied JSON credentials to sign in at the loopback port without exposing login data."""
    try:
        headers = {**REQUEST_MARKER, "Content-Type": "application/json"}
        with local_response(port, "POST", LOGIN, headers, credentials) as response:
            if response.status != 200:
                return None, {"error": "http_status", "httpStatus": response.status}
            cookie = workspace_cookie(response.headers)
            return (cookie, None) if cookie else (None, {"error": "invalid_login_response"})
    except (OSError, HTTPException):
        return None, {"error": "login_unreachable"}
    except (CookieError, ValueError):
        return None, {"error": "invalid_login_response"}


def sign_out(port, cookie):
    """Revoke the CLI's cookie at the loopback port and return only a safe error, if any."""
    try:
        with local_response(port, "POST", LOGOUT, {**REQUEST_MARKER, "Cookie": cookie}) as response:
            if response.status != 204:
                return {"error": "http_status", "httpStatus": response.status}
    except (OSError, HTTPException, ValueError):
        return {"error": "logout_unreachable"}
    return None
