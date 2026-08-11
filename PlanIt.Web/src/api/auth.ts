import { delay } from "./mockClient";
import type { User } from "../types/domain";

export interface MockLoginResult {
  user: User;
  accessToken: string;
  expiresInSeconds: number;
}

export async function mockLogin(user: User): Promise<MockLoginResult> {
  return delay({
    user,
    accessToken: `mock-token-${user.id}-${Date.now()}`,
    expiresInSeconds: 15 * 60,
  });
}
