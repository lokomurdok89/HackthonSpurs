"""Private deterministic replacement for the SPUR reader in the browser test only."""
from http.server import BaseHTTPRequestHandler, HTTPServer
import json

TEXT = "Part SAMPLE-17\nQuantity: 42\n<b>literal text</b>"


class Stub(BaseHTTPRequestHandler):
    def do_POST(self):
        """Check the private image-reader request and return deterministic multiline evidence."""
        image = self.rfile.read(int(self.headers["Content-Length"]))
        assert self.path == "/internal/read"
        page = int(self.headers["X-Page-Number"])
        assert page > 0
        assert self.headers["X-Reader-Contract"] == "1"
        png = self.headers["Content-Type"] == "image/png"
        assert png or self.headers["Content-Type"] == "image/jpeg"
        assert image.startswith(bytes.fromhex("89504e470d0a1a0a" if png else "ffd8ff"))
        assert len(image) < 80000
        print(self.headers["X-Document-Id"], page, flush=True)  # Only synthetic receipt IDs, never payloads.
        loc = {"kind": "page", "page": page}
        result = {"documentId": self.headers["X-Document-Id"], "version": "1", "readerVersion": "spur/1:offline-test",
                  "fragments": [{"method": "ocr", "locator": loc, "text": f"Rendered page {page}" if png and image[16:24] != bytes.fromhex("0000000100000001") else TEXT}], "warnings": []}
        if png and int.from_bytes(image[16:20], "big") == 750:
            assert int.from_bytes(image[20:24], "big") == 420 and image[24:26] == bytes([1, 0])
            offset, chunks = 8, []
            while offset < len(image):
                chunks.append(image[offset + 4:offset + 8])
                offset += 12 + int.from_bytes(image[offset:offset + 4], "big")
            assert offset == len(image) and set(chunks) == {b"IHDR", b"IDAT", b"IEND"}
            result["fragments"][0]["text"] = "Bilevel sheet"
        for marker in ("INDEX-BLANK", "INDEX-REJECT", "INDEX-RETRY", "INDEX-INCOMPATIBLE", "INDEX-LEASE"):
            if png and image.endswith(marker.encode()):
                if marker == "INDEX-BLANK":
                    result["fragments"] = []
                    result["warnings"] = [{"code": "no_readable_text", "message": "Blank page", "locator": loc}]
                else:
                    result["fragments"][0]["text"] = marker
                break
        status = 200
        if png and image.endswith(b"OCR-CREDENTIAL-FAILURE"):
            status = 422
            result = {"code": "reader_credentials_rejected", "detail": "private simulated provider body"}
        body = json.dumps(result).encode()
        self.send_response(status)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)


if __name__ == "__main__":
    HTTPServer(("0.0.0.0", 8080), Stub).serve_forever()
