"""Show safe reading and indexing diagnostics for an existing local receipt."""

import argparse
from http.client import HTTPConnection, HTTPException
import json
from uuid import UUID


READING_STATES = {"Queued", "Processing", "Completed", "Failed"}
INDEX_STATES = READING_STATES | {"AwaitingReading", "NotApplicable", "NotIndexed", "Skipped"}

# Only codes traced to their emission points in the Worker or private reader.
READING_FAILURES = {
    "original_mismatch": "original_verification",
    "original_unavailable": "original_verification",
    "unsupported_document": "render_preflight",
    "too_many_pages": "render_preflight",
    "renderer_unavailable": "render_tool",
    "document_not_renderable": "render_or_scratch_io",
    "invalid_reader_request": "page_reader_input",
    "unsupported_image_type": "page_reader_input",
    "length_required": "page_reader_input",
    "image_too_large": "page_reader_input",
    "empty_image": "page_reader_input",
    "incomplete_image": "page_reader_input",
    "reader_credentials_rejected": "ocr_gateway",
    "reader_request_rejected": "ocr_gateway",
    "reader_timeout": "reader_or_gateway",
    "reader_unavailable": "reader_or_gateway",
    "invalid_reader_result": "reading_validation",
    "document_timeout": "unknown",
    "reader_rejected": "unknown",
    "lease_expired": "unknown",
}
INDEX_FAILURES = {
    "index_source_invalid": "index_input",
    "embedding_provider_timeout": "index_embedding",
    "embedding_provider_unavailable": "index_embedding",
    "embedding_provider_rejected": "index_embedding",
    "embedding_response_invalid": "index_embedding",
    "index_lease_expired": "unknown",
}
REQUEST_TIMEOUT_SECONDS = 5
MAX_STATUS_BYTES = 8192


def project_status(payload, receipt, states, failure_stages):
    """Validate a response for receipt against the lane's states and allowlist failure_stages."""
    if not isinstance(payload, dict) or payload.get("id") != receipt:
        raise ValueError("Unexpected receipt response.")
    state, attempt = payload.get("state"), payload.get("attempt")
    if not isinstance(state, str) or state not in states or type(attempt) is not int or attempt < 0:
        raise ValueError("Unexpected receipt state or attempt.")

    status = {"state": state, "attempt": attempt}
    if state == "Failed":
        code = payload.get("failureCode")
        if not isinstance(code, str) or code not in failure_stages:
            code = None
        status.update(failureCode=code, failureStage=failure_stages.get(code, "unknown"))
    return status


def unique_fields(pairs):
    """Reject duplicate field names in the decoded JSON object pairs."""
    result = dict(pairs)
    if len(result) != len(pairs):
        raise ValueError("Duplicate status response fields.")
    return result


def read_status(port, path, receipt, states, failure_stages):
    """Read one loopback path at port and project only validated receipt status for its lane."""
    connection = HTTPConnection("127.0.0.1", port, timeout=REQUEST_TIMEOUT_SECONDS)
    try:
        connection.request("GET", path, headers={"Accept": "application/json"})
        with connection.getresponse() as response:
            if response.status != 200:
                return {"error": "http_status", "httpStatus": response.status}
            if response.headers.get_content_type() != "application/json":
                return {"error": "invalid_status_response"}
            body = response.read(MAX_STATUS_BYTES + 1)
        if len(body) > MAX_STATUS_BYTES:
            return {"error": "invalid_status_response"}
        payload = json.loads(body, object_pairs_hook=unique_fields)
        return project_status(payload, receipt, states, failure_stages)
    except (OSError, HTTPException):
        return {"error": "status_unreachable"}
    except (ValueError, RecursionError):
        return {"error": "invalid_status_response"}
    finally:
        connection.close()


def receipt_id(value):
    """Normalize a supplied UUID value; never echo invalid input."""
    try:
        return str(UUID(value))
    except ValueError:
        raise argparse.ArgumentTypeError("Use an existing receipt UUID.") from None


def api_port(value):
    """Parse the supplied TCP port value without echoing invalid input."""
    try:
        port = int(value)
        if 1 <= port <= 65535:
            return port
    except ValueError:
        pass
    raise argparse.ArgumentTypeError("Use a TCP port from 1 to 65535.")


def main():
    """Read the selected state projections once and write a sanitized JSON report."""
    parser = argparse.ArgumentParser(description=__doc__, allow_abbrev=False)
    parser.add_argument("receipt", type=receipt_id)
    parser.add_argument("--port", type=api_port, default=5080, help="Local API_PORT (default: 5080).")
    parser.add_argument("--with-index", action="store_true", help="Also query a deployment with index-status support.")
    args, extra = parser.parse_known_args()
    if extra:
        parser.error("Unexpected arguments.")

    path = f"/api/documents/{args.receipt}"
    report = {
        "reading": read_status(args.port, path, args.receipt, READING_STATES, READING_FAILURES),
    }
    if args.with_index:
        report["index"] = read_status(args.port, path + "/index-status", args.receipt, INDEX_STATES, INDEX_FAILURES)
    print(json.dumps(report, indent=2))
    return int(any("error" in status for status in report.values()))


if __name__ == "__main__":
    raise SystemExit(main())
