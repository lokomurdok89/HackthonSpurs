"""Black-box tests. Start a private, disposable Compose project with invented inputs only."""
import concurrent.futures
import hashlib
import json
import os
from pathlib import Path
import secrets
import subprocess
import time
import unittest
import urllib.error
import urllib.request
import uuid
from document_fixtures import form, workbook

ROOT = Path(__file__).resolve().parents[2]
SPEC = json.loads((ROOT / "docs/api/openapi.json").read_text(encoding="utf-8"))
RESULT_EXAMPLES = SPEC["paths"]["/api/documents/{id}/result"]["get"]["responses"]["200"]["content"]["application/json"]["examples"]
LIMIT = 32768  # Small test-only limit; the API default is 20 MiB.
PROJECT = "spurs-test-" + uuid.uuid4().hex[:12]
ENV = dict(os.environ, POSTGRES_PASSWORD=secrets.token_hex(24), API_PORT="0", UPLOAD_MAX_BYTES=str(LIMIT),
           COMPOSE_FILE=os.pathsep.join([os.environ.get("COMPOSE_FILE", "compose.yaml"),
                                         "tests/integration/compose.test-base.yaml"]))
CSV = b'part,certificate\r\n"CAS-014","CERT-027"\r\n'
PDF = b"%PDF-1.4\n% synthetic signature fixture; no reader is invoked\n%%EOF\n"


def compose(*args, capture=False, stdin=None):
    """Run Compose args in the disposable project; capture requests stdout and stdin supplies optional input."""
    return subprocess.run(["docker", "compose", "--env-file", "tests/integration/compose.test.env", "-p", PROJECT, *args], cwd=ROOT, env=ENV,
                          input=stdin, text=True, check=True, stdout=subprocess.PIPE if capture else None).stdout


def sql(statement):
    """Execute statement in the disposable PostgreSQL database and return its scalar text."""
    return compose("exec", "-T", "postgres", "psql", "-XAtq", "-v", "ON_ERROR_STOP=1",
                   "-U", "documents", "-d", "documents", capture=True, stdin=statement).strip()


class DocumentsAPI(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        """Start a disposable API and database and register teardown."""
        # The generated project name isolates containers and volumes from developer data.
        cls.addClassCleanup(compose, "down", "-v", "--remove-orphans", "--rmi", "local")
        compose("up", "--build", "-d", "--wait")
        cls.ready()

    @classmethod
    def ready(cls):
        """Wait for the isolated API to return a real not-found response and refresh the test base URL."""
        # Docker may reassign an ephemeral host port when the API container restarts.
        cls.base = "http://" + compose("port", "api", "8080", capture=True).strip()
        for _ in range(80):
            try:
                if cls.http("/api/documents/" + str(uuid.uuid4()))[0] == 404:
                    return
            except (OSError, ValueError):
                pass
            time.sleep(0.5)
        raise AssertionError("API did not become available")

    @classmethod
    def http(cls, url, body=None, headers=None):
        """Call the relative url with optional body and headers, returning status, headers and decoded JSON."""
        req = urllib.request.Request(cls.base + url, body, headers or {})
        try:
            response = urllib.request.urlopen(req, timeout=20)
        except urllib.error.HTTPError as err:
            response = err
        with response:
            return response.status, response.headers, json.load(response)

    def upload(self, key=None, data=CSV, mime="text/csv", name="input.csv"):
        """Submit data as name with the declared mime type and optional idempotency key."""
        body, typ = form([("file", name, mime, data)])
        return self.http("/api/documents", body, {"Content-Type": typ, "Idempotency-Key": key or str(uuid.uuid4())})

    def problem(self, response, status, code):
        """Assert that response matches the expected HTTP status and public problem code."""
        actual, headers, payload = response
        self.assertEqual((actual, headers.get_content_type(), payload["status"], payload["code"]),
                         (status, "application/problem+json", status, code))
        self.assertTrue(payload["title"])

    def test_idempotent_upload_and_original(self):
        """Verify byte-identical replay returns one receipt and the stored original matches its digest."""
        key = str(uuid.uuid4())
        status, headers, receipt = self.upload(key)
        self.assertEqual(status, 202)
        doc = str(uuid.UUID(receipt["id"]))
        url = headers["Location"]
        self.assertEqual(url, "/api/documents/" + doc)
        self.assertEqual(self.upload(key, name="../renamed.csv")[2], receipt)
        self.problem(self.upload(key, CSV + b"another,row\n"), 409, "idempotency_conflict")
        other = self.upload()[2]
        self.assertNotEqual(other, receipt)
        self.assertEqual(self.http(url)[2], {"id": doc, "state": "Queued", "attempt": 0})
        self.problem(self.http(url + "/result"), 409, "result_not_ready")
        hash_value = compose("exec", "-T", "api", "sha256sum", "/data/originals/" + doc.replace("-", "") + ".original", capture=True).split()[0]
        self.assertEqual(hash_value, hashlib.sha256(CSV).hexdigest())

    def test_concurrent_replay_and_conflict(self):
        """Race equal and conflicting uploads; verify one winner, one original and no temporary residue."""
        for variants in ([CSV] * 8, [CSV, b"part,other\n"] * 4):
            key = str(uuid.uuid4())
            with concurrent.futures.ThreadPoolExecutor(max_workers=8) as pool:
                responses = list(pool.map(lambda data: self.upload(key, data), variants))
            ids = {r[2]["id"] for r in responses if r[0] == 202}
            self.assertEqual(len(ids), 1)
            winning_bytes = next(data for data, r in zip(variants, responses) if r[0] == 202)
            for data, response in zip(variants, responses):
                if data == winning_bytes:
                    self.assertEqual(response[0], 202)
                else:
                    self.problem(response, 409, "idempotency_conflict")
            self.assertEqual(sql(f"""SELECT count(*) FROM document_jobs WHERE "OperationKey"='{key}';"""), "1")
        files = compose("exec", "-T", "api", "ls", "-1", "/data/originals", capture=True).splitlines()
        self.assertFalse(any(f.endswith(".part") for f in files))
        self.assertEqual(sum(f.endswith(".original") for f in files), int(sql("SELECT count(*) FROM document_jobs;")))

    def test_input_validation(self):
        """Verify valid fixtures are accepted and malformed, oversized or mismatched requests are rejected."""
        for data, mime in ((PDF, "application/pdf"), (workbook(), "application/octet-stream"), (CSV, "application/octet-stream")):
            self.assertEqual(self.upload(data=data, mime=mime)[0], 202)
        for data, mime in ((CSV, "application/pdf"), (PDF, "text/csv"), (b"PK\x03\x04truncated", "application/octet-stream"),
                           (b"\x00x,y", "text/csv"), (b'abc,"unfinished', "text/csv")):
            self.problem(self.upload(data=data, mime=mime), 415, "unsupported_media_type")
        self.problem(self.upload(data=b""), 400, "invalid_request")
        self.problem(self.upload(data=b"x," + b"a" * LIMIT), 413, "file_too_large")
        self.assertEqual(self.upload(data=b"x," + b"a" * (LIMIT - 2))[0], 202)
        part = ("file", "input.csv", "text/csv", CSV)
        for parts in ([], [part, part], [("wrong", *part[1:])]):
            data, mime = form(parts)
            self.problem(self.http("/api/documents", data, {"Content-Type": mime, "Idempotency-Key": str(uuid.uuid4())}), 400, "invalid_request")
        data, mime = form([part])
        for headers in ({}, {"Idempotency-Key": "not-a-uuid"}):
            self.problem(self.http("/api/documents", data, {"Content-Type": mime, **headers}), 400, "invalid_request")
        headers = {"Idempotency-Key": str(uuid.uuid4())}
        self.problem(self.http("/api/documents", CSV, {"Content-Type": "text/csv", **headers}), 415, "unsupported_media_type")
        self.problem(self.http("/api/documents", data[:-40], {"Content-Type": mime, **headers}), 400, "invalid_request")

    def test_query_state_matrix_and_durable_result(self):
        """Verify status and result projections are side-effect free and survive service restart."""
        for suffix in ("", "/result"):
            self.problem(self.http("/api/documents/bad-id" + suffix), 400, "invalid_document_id")
            self.problem(self.http("/api/documents/" + str(uuid.uuid4()) + suffix), 404, "document_not_found")
        key = str(uuid.uuid4())
        doc = str(uuid.UUID(self.upload(key)[2]["id"]))
        url = "/api/documents/" + doc
        # Fixture-only SQL stands in for the future Worker; never a public write route.
        snapshot = f"""SELECT to_jsonb(j)::text FROM document_jobs j WHERE "Id"='{doc}';"""
        for state in ("Processing", "Queued", "Failed"):
            failure = "'reader_timeout'" if state == "Failed" else "NULL"
            sql(f"""UPDATE document_jobs SET "Attempt"=1, "State"='{state}', "FailureCode"={failure} WHERE "Id"='{doc}';""")
            expected = {"id": doc, "state": state, "attempt": 1}
            if state == "Failed":
                expected["failureCode"] = "reader_timeout"
            before = sql(snapshot)
            self.assertEqual(self.http(url)[2], expected)
            self.problem(self.http(url + "/result"), 409, "processing_failed" if state == "Failed" else "result_not_ready")
            self.assertEqual(sql(snapshot), before, "Queries must not change the job or lease")
        for name in ("materialsRow", "unconfirmedLink", "originNotDemonstrated", "unreadableSection", "csvCell"):
            fixture = dict(RESULT_EXAMPLES[name]["value"], documentId=doc)
            sql(f"""UPDATE document_jobs SET "State"='Completed', "FailureCode"=NULL, "ResultJson"=$fixture${json.dumps(fixture)}$fixture$ WHERE "Id"='{doc}';""")
            status, headers, result = self.http(url + "/result")
            self.assertEqual((status, headers.get_content_type(), result), (200, "application/json", fixture))
        compose("restart", "api", "postgres")
        self.ready()
        self.assertEqual(self.http(url)[2], {"id": doc, "state": "Completed", "attempt": 1})
        self.assertEqual(self.http(url + "/result")[2], fixture)
        self.assertEqual(self.upload(key)[2]["id"], doc)

    def test_runtime_database_outage(self):
        """Verify reads and writes report temporary unavailability while PostgreSQL is offline."""
        doc = self.upload()[2]["id"]
        compose("stop", "postgres")
        try:
            self.problem(self.http("/api/documents/" + doc), 503, "service_unavailable")
            self.problem(self.upload(), 503, "service_unavailable")
        finally:
            compose("start", "postgres")
            self.ready()


if __name__ == "__main__":
    unittest.main(verbosity=2)
