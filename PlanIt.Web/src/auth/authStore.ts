import type { User } from "../types/domain";

// Plain module-level singleton, not React Context — both httpClient.ts's fetch wrapper and
// SignalR's accessTokenFactory need synchronous access to "the current token" outside of any
// component tree.
//
// This module deliberately has no dependency on api/auth.ts (or anything else in api/), even
// though it needs to call the real /auth/refresh endpoint on a timer: api/httpClient.ts already
// imports getAccessToken/clearSession from here, so importing api/auth.ts here too would create
// a circular module dependency. Instead, main.tsx calls configureRefresh() once at startup to
// inject the refresh function, keeping this module import-free of the API layer.

export interface RefreshResult {
  user: User;
  accessToken: string;
  expiresInSeconds: number;
  refreshToken: string;
}

type RefreshFn = (refreshToken: string) => Promise<RefreshResult>;

let accessToken: string | null = null;
let currentUser: User | null = null;
let refreshToken: string | null = null;
let refreshTimer: ReturnType<typeof setTimeout> | null = null;
let refreshFn: RefreshFn | null = null;
const listeners = new Set<() => void>();

const REFRESH_TOKEN_KEY = "planit:refreshToken";

function notify() {
  listeners.forEach((listener) => listener());
}

export function subscribe(listener: () => void): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

export function configureRefresh(fn: RefreshFn) {
  refreshFn = fn;
}

export function getAccessToken(): string | null {
  return accessToken;
}

export function getRefreshToken(): string | null {
  return refreshToken;
}

export function getCurrentUser(): User | null {
  return currentUser;
}

export function isAuthenticated(): boolean {
  return accessToken !== null && currentUser !== null;
}

// Proactive/timer-based, not reactive to a 401 or SignalR reconnect — fires at 80% of the access
// token's lifetime, needed because a user can sit idle on SignalR-only updates with zero
// outgoing REST calls, which would never trigger a reactive refresh (planit-api-contracts-backend.md §4).
function scheduleRefresh(expiresInSeconds: number) {
  if (refreshTimer) clearTimeout(refreshTimer);
  refreshTimer = setTimeout(() => {
    void performProactiveRefresh();
  }, expiresInSeconds * 0.8 * 1000);
}

async function performProactiveRefresh() {
  if (!refreshToken || !refreshFn) return;
  try {
    const result = await refreshFn(refreshToken);
    setSession(result.user, result.accessToken, result.expiresInSeconds, result.refreshToken);
  } catch {
    // Refresh token expired, revoked, or reused — force back to /login rather than looping.
    clearSession();
  }
}

export function setSession(user: User, token: string, expiresInSeconds: number, newRefreshToken: string) {
  currentUser = user;
  accessToken = token;
  refreshToken = newRefreshToken;
  scheduleRefresh(expiresInSeconds);
  // Only the refresh token is persisted — the access token stays in-memory only, per the
  // master plan's auth model. The refresh token surviving tab/browser close is what makes
  // "return to last project" possible without a fresh login every time.
  localStorage.setItem(REFRESH_TOKEN_KEY, newRefreshToken);
  notify();
}

export function clearSession() {
  currentUser = null;
  accessToken = null;
  refreshToken = null;
  if (refreshTimer) clearTimeout(refreshTimer);
  localStorage.removeItem(REFRESH_TOKEN_KEY);
  notify();
}

export function getStoredRefreshToken(): string | null {
  return localStorage.getItem(REFRESH_TOKEN_KEY);
}
