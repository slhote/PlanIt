import type { User } from "../types/domain";

// Plain module-level singleton, not React Context — both the future REST fetch
// wrapper and SignalR's accessTokenFactory need synchronous access to "the
// current token" outside of any component tree. Mocked here (no real refresh
// call), but the shape (get/set/refresh + timer) is what a real implementation
// keeps.

let accessToken: string | null = null;
let currentUser: User | null = null;
let refreshTimer: ReturnType<typeof setTimeout> | null = null;
const listeners = new Set<() => void>();

const LAST_USER_KEY = "planit:currentUserId";

function notify() {
  listeners.forEach((listener) => listener());
}

export function subscribe(listener: () => void): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

export function getAccessToken(): string | null {
  return accessToken;
}

export function getCurrentUser(): User | null {
  return currentUser;
}

export function isAuthenticated(): boolean {
  return accessToken !== null && currentUser !== null;
}

function scheduleRefresh(expiresInSeconds: number) {
  if (refreshTimer) clearTimeout(refreshTimer);
  // TODO: SignalR's accessTokenFactory reads getAccessToken() directly, so once
  // the hub exists it needs no refresh logic of its own — it rides this timer.
  refreshTimer = setTimeout(() => {
    if (!currentUser) return;
    setSession(currentUser, `mock-token-${currentUser.id}-${Date.now()}`, 15 * 60);
  }, expiresInSeconds * 0.8 * 1000);
}

export function setSession(user: User, token: string, expiresInSeconds: number) {
  currentUser = user;
  accessToken = token;
  scheduleRefresh(expiresInSeconds);
  localStorage.setItem(LAST_USER_KEY, user.id);
  notify();
}

export function clearSession() {
  currentUser = null;
  accessToken = null;
  if (refreshTimer) clearTimeout(refreshTimer);
  localStorage.removeItem(LAST_USER_KEY);
  notify();
}

export function getRememberedUserId(): string | null {
  return localStorage.getItem(LAST_USER_KEY);
}
