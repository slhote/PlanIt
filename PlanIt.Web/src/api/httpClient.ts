// Real fetch-based transport, replacing mockClient.ts's role. Per PlanIt.Web/CLAUDE.md's own
// framing of the mock layer ("a real implementation should swap this file's guts for fetch() and
// keep the same exported function signatures"), api/*.ts call apiFetch instead of delay()/mutate(),
// but keep the same shape callers already depend on.

import { clearSession, getAccessToken } from "../auth/authStore";
import { getConnectionId } from "../realtime/connectionId";

const BASE_URL = import.meta.env.VITE_API_BASE_URL as string;

export class ApiError extends Error {
  status: number;
  constructor(message: string, status: number) {
    super(message);
    this.status = status;
  }
}

interface ApiFetchOptions extends RequestInit {
  /** Skip the Authorization header — for /auth/register and /auth/login, which have no session yet. */
  skipAuth?: boolean;
}

export async function apiFetch<T>(path: string, options: ApiFetchOptions = {}): Promise<T> {
  const { skipAuth, headers, ...init } = options;

  const requestHeaders = new Headers(headers);
  if (init.body) {
    requestHeaders.set("Content-Type", "application/json");
  }
  if (!skipAuth) {
    const token = getAccessToken();
    if (token) {
      requestHeaders.set("Authorization", `Bearer ${token}`);
    }
  }
  const connectionId = getConnectionId();
  if (connectionId) {
    requestHeaders.set("X-SignalR-Connection-Id", connectionId);
  }

  const response = await fetch(`${BASE_URL}${path}`, { ...init, headers: requestHeaders });

  if (!response.ok) {
    // A 401 on an authenticated call (not login/register itself) means the session is no
    // longer valid — clear it so RequireAuth redirects to /login, rather than surfacing a raw
    // fetch error into whatever UI happened to trigger the call.
    if (response.status === 401 && !skipAuth) {
      clearSession();
    }

    const problem = await response.json().catch(() => null);
    throw new ApiError(problem?.detail ?? problem?.title ?? response.statusText, response.status);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}
