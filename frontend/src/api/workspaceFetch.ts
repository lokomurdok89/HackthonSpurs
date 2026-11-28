export const workspaceUnauthorized = "workspace:unauthorized";
let generation = 0;

/** Keep responses from a former browser session from locking a newly signed-in session. */
export function invalidateWorkspaceRequests() {
  generation += 1;
}

/** Fetch a same-origin API path with its workspace cookie and write protection.
 * @param path - Same-origin API URL.
 * @param init - Optional fetch settings.
 * @param notify - Whether a 401 should invalidate the current workspace UI.
 */
export async function workspaceFetch(path: string, init?: RequestInit, notify = true) {
  const requestedGeneration = generation;
  const headers = new Headers(init?.headers);
  const method = init?.method?.toUpperCase() ?? "GET";
  if (!["GET", "HEAD", "OPTIONS", "TRACE"].includes(method)) headers.set("X-Workspace-Request", "1");
  const response = await fetch(path, { ...init, credentials: "same-origin", headers });
  if (response.status === 401 && notify && requestedGeneration === generation && !init?.signal?.aborted) {
    window.dispatchEvent(new Event(workspaceUnauthorized));
  }
  return response;
}
