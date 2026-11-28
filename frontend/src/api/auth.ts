import { toProblem } from "./client";
import { ApiError } from "./errors";
import { workspaceFetch } from "./workspaceFetch";

export type WorkspaceRole = "admin" | "reader";

export interface WorkspaceUser {
  id: string;
  username: string;
  displayName: string;
  role: WorkspaceRole;
  active: boolean;
}

export interface CreateUser {
  username: string;
  displayName: string;
  password: string;
  role: WorkspaceRole;
}

export type UpdateUser = Partial<Omit<CreateUser, "username"> & { active: boolean }>;

export const accountLimits = { username: 64, displayName: 120, minPassword: 12, password: 128 };
export const roleLabel: Record<WorkspaceRole, string> = { admin: "Administrator", reader: "Reader" };

/** Parse a workspace response without exposing raw server errors.
 * @param response - Fetch result from a workspace operation.
 */
async function expectOk(response: Response) {
  if (!response.ok) throw new ApiError(await toProblem(response));
  return response;
}

/** Read the current server-verified identity, optionally cancelling with the supplied signal. */
export async function getCurrentUser(signal?: AbortSignal): Promise<WorkspaceUser> {
  const response = await expectOk(await workspaceFetch("/api/auth/me", { signal }, false));
  return response.json() as Promise<WorkspaceUser>;
}

/** Sign in using the supplied login name and unmodified password. */
export async function login(username: string, password: string): Promise<WorkspaceUser> {
  return sendJson("/api/auth/login", "POST", { username, password }, false);
}

/** Revoke the current session, treating a session already gone as signed out. */
export async function logout(): Promise<void> {
  const response = await workspaceFetch("/api/auth/logout", { method: "POST" }, false);
  if (response.status !== 401) await expectOk(response);
}

/** List workspace users, optionally cancelling the admin's read with the supplied signal. */
export async function listUsers(signal?: AbortSignal): Promise<WorkspaceUser[]> {
  const response = await expectOk(await workspaceFetch("/api/users", { signal }));
  return response.json() as Promise<WorkspaceUser[]>;
}

/** Create a new active workspace account from the supplied admin form fields. */
export function createUser(user: CreateUser): Promise<WorkspaceUser> {
  return sendJson("/api/users", "POST", user);
}

/** Apply the given changes only to the workspace user identified by id. */
export function updateUser(id: string, changes: UpdateUser): Promise<WorkspaceUser> {
  return sendJson(`/api/users/${encodeURIComponent(id)}`, "PATCH", changes);
}

/** Send a JSON account mutation to the given path and decode its public user summary.
 * @param path - Same-origin API route.
 * @param method - Account write method.
 * @param payload - Login, create or update fields.
 * @param notify - Whether 401 invalidates the current session UI.
 */
async function sendJson(path: string, method: "POST" | "PATCH", payload: object, notify = true): Promise<WorkspaceUser> {
  const response = await expectOk(await workspaceFetch(path, {
    method,
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload),
  }, notify));
  return response.json() as Promise<WorkspaceUser>;
}
