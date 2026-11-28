"""Deterministic, private stand-in for SPUR embeddings. Handles synthetic text only."""
from collections import Counter
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import json
from threading import Lock
import time

COUNTS = Counter()
LOCK = Lock()
VECTORS = {
    "Rendered page 1": [1, 0.1, 0.1],
    "Rendered page 2": [0.1, 1, 0.1],
    "INDEX-LEASE": [0.1, 1, 0.1],
}


class Stub(BaseHTTPRequestHandler):
    def reply(self, status, payload):
        """Return JSON payload with HTTP status and known response length."""
        body = json.dumps(payload).encode()
        self.send_response(status)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        try:
            self.wfile.write(body)
        except (BrokenPipeError, ConnectionResetError):
            pass

    def do_GET(self):
        """Expose only synthetic request counts to assertions on the private network."""
        assert self.path == "/requests"
        with LOCK:
            self.reply(200, COUNTS)

    def do_POST(self):
        """Validate the expected test-only embedding request and simulate success and failures."""
        assert self.path == "/v1/embeddings"
        assert self.headers["Authorization"] == "Bearer unused-test-only"
        body = json.loads(self.rfile.read(int(self.headers["Content-Length"])))
        texts = body["input"]
        assert body["model"] == "test-model" and 0 < len(texts) <= 8
        with LOCK:
            COUNTS.update(texts)
            first_count = COUNTS[texts[0]]

        text = texts[0]
        if text in ("INDEX-REJECT", "SEARCH-REJECT"):
            self.reply(400, {"internal": "provider secret-shaped diagnostic"})
            return
        if text == "INDEX-RETRY" and first_count < 3:
            self.reply(503, {"internal": "temporary synthetic outage"})
            return
        if text == "INDEX-INCOMPATIBLE":
            self.reply(200, {"data": [{"index": 0, "embedding": [1, 1]}]})
            return
        if text == "INDEX-LEASE" and first_count == 1:
            time.sleep(4)
            self.reply(200, {"data": [{"index": 0, "embedding": [0.1, 0.1, 1]}]})
            return

        data = [{"index": i, "embedding": VECTORS.get(t, [0.1, 0.1, 1])} for i, t in enumerate(texts)]
        self.reply(200, {"data": list(reversed(data))})


if __name__ == "__main__":
    ThreadingHTTPServer(("0.0.0.0", 8080), Stub).serve_forever()
