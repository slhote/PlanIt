import { apiFetch } from "./httpClient";
import type { User } from "../types/domain";

export async function searchUsers(query: string): Promise<User[]> {
  const q = query.trim();
  if (!q) return [];
  return apiFetch<User[]>(`/users/search?q=${encodeURIComponent(q)}`);
}
