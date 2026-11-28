"""Exercise actual worker ownership, retries and public reads on a disposable database."""
import subprocess
import time
import unittest
import uuid

import test_document_api as api
from test_document_api import compose, sql

FIXTURE = (api.ROOT / "backend/Testing/HackthonSpurs.Reader.Fixture/worker-demo.csv").read_bytes()


def upload(data=FIXTURE, key=None):
    """Submit data with an optional key and return the accepted UUID."""
    body, typ = api.form([("file", "example.csv", "text/csv", data)])
    status, _, receipt = api.DocumentsAPI.http("/api/documents", body, {
        "Content-Type": typ, "Idempotency-Key": key or str(uuid.uuid4())})
    assert status == 202
    return str(uuid.UUID(receipt["id"]))


def wait_state(doc, state, attempt, seconds=20):
    """Poll doc for the expected state and attempt, failing after seconds elapse."""
    until = time.monotonic() + seconds
    while time.monotonic() < until:
        status = api.DocumentsAPI.http("/api/documents/" + doc)[2]
        if (status["state"], status["attempt"]) == (state, attempt):
            return status
        time.sleep(.1)
    raise AssertionError(f"Expected {state}/{attempt}, last: {status}")


class DocumentWorker(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        """Start the synthetic Worker stack and wait for the test API."""
        cls.addClassCleanup(compose, "--profile", "synthetic", "down", "-v", "--remove-orphans", "--rmi", "local")
        compose("--profile", "synthetic", "up", "--build", "-d", "--wait")
        api.DocumentsAPI.ready()

    def test_bytes_not_filename_and_idempotent_publication(self):
        """Verify only allowlisted bytes are read and replay produces the same persisted result."""
        other = upload(FIXTURE.replace(b"014", b"015"))
        key = str(uuid.uuid4())
        doc = upload(key=key)
        wait_state(doc, "Completed", 1)
        self.assertEqual(doc, upload(key=key))
        result = api.DocumentsAPI.http("/api/documents/" + doc + "/result")[2]
        self.assertEqual((result["documentId"], result["readerVersion"]), (doc, "synthetic-fixture/1"))
        self.assertEqual(result["fragments"], [{"text": "DEMO-ONLY-014", "method": "native",
                         "locator": {"kind": "csvCell", "row": 2, "column": 1}}])
        wait_state(other, "Queued", 0)

    def test_crash_then_reclaim_and_reject_expired_reply(self):
        """Transfer a lease during reading and verify its previous owner cannot publish stale evidence."""
        compose("pause", "fixture-reader")
        try:
            doc = upload()
            wait_state(doc, "Processing", 1)
            compose("kill", "-s", "SIGKILL", "worker")
            # Advance database deadlines instead of waiting 30 seconds per lease.
            def expire():
                """Advance the selected job lease deadline without waiting for wall-clock expiry."""
                sql(f"""UPDATE document_jobs SET "LeaseUntil"=now()-interval '1 second' WHERE "Id"='{doc}';""")
            expire()
            # Compose start also starts dependencies; the reader must remain paused here.
            worker = compose("ps", "-a", "-q", "worker", capture=True).strip()
            subprocess.run(["docker", "start", worker], check=True)
            wait_state(doc, "Processing", 2)
            # Transfer ownership while the actual HTTP request is in flight.
            new_owner = str(uuid.uuid4())
            sql(f"""UPDATE document_jobs SET "LeaseToken"='{new_owner}', "LeaseUntil"=now()+interval '1 minute'
                  WHERE "Id"='{doc}';""")
        finally:
            compose("unpause", "fixture-reader")
        until = time.monotonic() + 15
        while time.monotonic() < until:
            if f"Job {doc}, attempt 2: lease_lost" in compose("logs", "worker", capture=True):
                break
            time.sleep(.2)
        else:
            self.fail("Worker did not report rejecting the stale publication")
        self.assertEqual(api.DocumentsAPI.http("/api/documents/" + doc + "/result")[2]["code"], "result_not_ready")
        expire()
        wait_state(doc, "Completed", 3)

    def test_expired_last_claim_does_not_get_a_fourth_attempt(self):
        """Verify an expired final lease fails without a fourth reader call."""
        compose("stop", "worker")
        try:
            doc = upload()
            sql(f"""UPDATE document_jobs SET "State"='Processing', "Attempt"=3,
              "LeaseToken"=gen_random_uuid(), "LeaseUntil"=now()-interval '1 second' WHERE "Id"='{doc}';""")
        finally:
            compose("start", "worker")
        self.assertEqual(wait_state(doc, "Failed", 3)["failureCode"], "lease_expired")
        self.assertEqual(api.DocumentsAPI.http("/api/documents/" + doc + "/result")[2]["code"], "processing_failed")

    def test_no_reader_retries_then_fails(self):
        """Check database-scheduled backoff and terminal failure when the fixture reader is unavailable."""
        compose("stop", "fixture-reader")
        try:
            doc = upload()
            for attempt, delay in [(1, 5), (2, 30)]:
                wait_state(doc, "Queued", attempt)
                remaining = float(sql(f"""SELECT extract(epoch from "NextAttemptAt"-now()) FROM document_jobs
                    WHERE "Id"='{doc}';"""))
                self.assertGreater(remaining, delay - 4)
                self.assertLessEqual(remaining, delay)
                sql(f"""UPDATE document_jobs SET "NextAttemptAt"=now() WHERE "Id"='{doc}';""")
            self.assertIn(wait_state(doc, "Failed", 3)["failureCode"], ("reader_unavailable", "reader_timeout"))
            self.assertEqual(api.DocumentsAPI.http("/api/documents/" + doc + "/result")[2]["code"], "processing_failed")
        finally:
            compose("start", "fixture-reader")

    def test_tampered_original_is_never_published(self):
        """Alter the stored original and verify the Worker rejects the hash mismatch."""
        compose("stop", "worker")
        try:
            doc = upload()
            path = "/data/originals/" + doc.replace("-", "") + ".original"
            compose("exec", "-T", "api", "sh", "-c", 'printf x | dd of="$1" conv=notrunc 2>/dev/null', "sh", path)
        finally:
            compose("start", "worker")
        self.assertEqual(wait_state(doc, "Failed", 1)["failureCode"], "original_mismatch")


if __name__ == "__main__":
    unittest.main(verbosity=2)
