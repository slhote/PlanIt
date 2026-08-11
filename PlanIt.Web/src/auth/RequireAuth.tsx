import type { ReactNode } from "react";
import { Navigate } from "react-router";
import { useAuth } from "./useAuth";

export function RequireAuth({ children }: { children: ReactNode }) {
  const { isAuthenticated } = useAuth();
  if (!isAuthenticated) {
    return <Navigate to="/login" replace />;
  }
  return <>{children}</>;
}
