import { seedUsers } from "./seedData";
import { delay } from "./mockClient";
import type { Guid, User } from "../types/domain";

export async function fetchUsers(): Promise<User[]> {
  return delay([...seedUsers]);
}

export async function fetchUser(userId: Guid): Promise<User | null> {
  return delay(seedUsers.find((u) => u.id === userId) ?? null);
}

export async function searchUsers(query: string): Promise<User[]> {
  const q = query.trim().toLowerCase();
  if (!q) return delay([]);
  return delay(
    seedUsers.filter(
      (u) => u.username.toLowerCase().includes(q) || u.email.toLowerCase().includes(q),
    ),
  );
}
