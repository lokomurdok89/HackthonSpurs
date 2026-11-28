# Workspace access and evaluation walkthrough

Origin Vault has one shared internal workspace. Accounts are local to the installation; there is no self-registration. A reader is an internal teammate, **not** an anonymous buyer. Do not put documents for unrelated customers in the same installation. The public buyer passport is a separate preview built from committed synthetic data; it does not expose an uploaded file, rendered page, account or search result.

| Capability | Anonymous | Reader | Administrator |
|---|---|---|---|
| Open a public synthetic passport link | Yes | Yes | Yes |
| Consult workspace views, a known receipt, its result and index status | No | Yes | Yes |
| Search the shared document index, including POST queries | No | Yes | Yes |
| Upload or send a document for OCR; use review editing controls | No | No | Yes |
| List/create users, edit roles, reset passwords, deactivate/reactivate | No | No | Yes |

## Set up the first administrator

1. In the private root Compose environment, set `Auth__BootstrapUsername` and `Auth__BootstrapPassword` alongside the database password. Choose a unique password of 12–128 characters and a username of 3–64 ASCII letters/digits, dots, hyphens or underscores, starting with a letter/digit. Do not use an example password, pass credentials in a URL or put either value in a `VITE_*` variable.
2. Start the [local API and UI](../README.md#run-locally). Sign in to the workspace. The initial administrator is created only when the user table is empty. Restarting is not a password-reset method. After verifying first login, remove the bootstrap settings and recreate **only the API** at your next controlled restart; retain the database volume. Never delete a volume to recover an account.
3. Open **Users** to create named accounts. Use **Reader** for reviewers who do not maintain the workspace. Keep a second authorized administrator for recovery. Deactivate accounts instead of deleting them. Users cannot deactivate or demote themselves; at least one active administrator must remain.

## Show it to an evaluator

- Open a private browser window at `/#/documents`: the sign-in screen appears before workspace data. Sign in as an administrator, open **Users**, create a reader, edit its display name and reload to verify persistence.
- Sign out and enter as the reader. Navigate to the same receipt or Search; there is no upload control. Opening `/#/users` directly does not grant access, and the API refuses user management even without the UI.
- Keep the reader window open. From a separate administrator session, reset the reader's password or deactivate the account. The previous cookie no longer authorizes requests. Reactivating does not restore the old session.
- Open `/#/p/EX-100?share=0&lots=0` in another private window. The synthetic buyer passport renders without signing in. It is not a link granting access to the internal workspace.

## API and operating limits

See the [OpenAPI contract](api/openapi.json). Sign in with a JSON body; clients retain the returned HttpOnly cookie and send it on subsequent same-origin calls. Every POST/PATCH, including login, logout, uploads and read-only POST searches, needs `X-Workspace-Request: 1`. Cross-origin credentialed requests are not supported. No raw session tokens or passwords belong in logs, browser storage, shell history or screenshots.

Sessions have a fixed eight-hour expiry. Logout revokes the current session; password, role and activation changes invalidate affected sessions. The server reads current account state rather than trusting browser-supplied roles. Authentication failures return 401, insufficient permissions or a missing request marker return 403, conflicts such as duplicate usernames return 409, and the per-origin login limit returns 429. Legacy receipt diagnostics that issue anonymous GETs will return 401; use the authenticated receipt view instead. The application does not offer public invitations, email recovery, SSO, MFA, customer isolation, or an audit console. Keep the demo on loopback; production hosting requires a separate TLS/proxy and access-control review.

**Repeatable evidence:** `python -B tests/integration/test_workspace_auth.py` exercises the actual HTTP API and PostgreSQL; `python -B tests/integration/test_workspace_ui.py` drives Chromium through login, account maintenance and reader navigation. Both provision private Compose projects with generated credentials and synthetic inputs.
