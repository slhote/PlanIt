import { apiFetch } from "./httpClient";
import type { User } from "../types/domain";

export interface AuthResult {
  user: User;
  accessToken: string;
  expiresInSeconds: number;
  refreshToken: string;
}

export async function register(username: string, email: string, password: string): Promise<AuthResult> {
  return apiFetch<AuthResult>("/auth/register", {
    method: "POST",
    skipAuth: true,
    body: JSON.stringify({ username, email, password }),
  });
}

export async function login(usernameOrEmail: string, password: string): Promise<AuthResult> {
  return apiFetch<AuthResult>("/auth/login", {
    method: "POST",
    skipAuth: true,
    body: JSON.stringify({ usernameOrEmail, password }),
  });
}

export async function refresh(refreshToken: string): Promise<AuthResult> {
  return apiFetch<AuthResult>("/auth/refresh", {
    method: "POST",
    skipAuth: true,
    body: JSON.stringify({ refreshToken }),
  });
}

export async function logout(refreshToken: string): Promise<void> {
  return apiFetch<void>("/auth/logout", {
    method: "POST",
    body: JSON.stringify({ refreshToken }),
  });
}
