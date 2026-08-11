import { useSyncExternalStore } from "react";
import { getCurrentUser, subscribe } from "./authStore";

export function useAuth() {
  const user = useSyncExternalStore(subscribe, getCurrentUser);
  return { user, isAuthenticated: user !== null };
}
