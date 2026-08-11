import { useMutation, useQuery } from "@tanstack/react-query";
import { useNavigate } from "react-router";
import { fetchUsers } from "../api/users";
import { mockLogin } from "../api/auth";
import { setSession } from "./authStore";
import { initials } from "../components/initials";
import type { User } from "../types/domain";

export function LoginPage() {
  const navigate = useNavigate();
  const usersQuery = useQuery({ queryKey: ["users"], queryFn: fetchUsers });

  const loginMutation = useMutation({
    mutationFn: (user: User) => mockLogin(user),
    onSuccess: (result) => {
      setSession(result.user, result.accessToken, result.expiresInSeconds);
      navigate("/", { replace: true });
    },
  });

  return (
    <div className="center-page">
      <div style={{ width: "100%", maxWidth: 480 }}>
        <div className="stack" style={{ marginBottom: "var(--space-6)", textAlign: "center" }}>
          <h1 style={{ fontSize: "var(--font-size-lg)" }}>PlanIt</h1>
          <p className="muted">
            No real accounts yet — pick a seeded teammate to explore the board as them.
          </p>
        </div>

        {usersQuery.isLoading && (
          <div className="loading-state">
            <div className="spinner" />
            <p>Loading teammates…</p>
          </div>
        )}

        {usersQuery.isError && (
          <div className="error-state">
            <p>Couldn't load the demo users. Try again.</p>
          </div>
        )}

        {usersQuery.data && (
          <div className="user-picker-grid">
            {usersQuery.data.map((user) => (
              <button
                key={user.id}
                type="button"
                className="user-pick-card"
                disabled={loginMutation.isPending}
                onClick={() => loginMutation.mutate(user)}
              >
                <span className="avatar">{initials(user.username)}</span>
                <span>
                  <div className="card-title">{user.username}</div>
                  <div className="card-meta">{user.email}</div>
                </span>
              </button>
            ))}
          </div>
        )}

        {loginMutation.isPending && (
          <div className="loading-state">
            <div className="spinner" />
            <p>Signing in…</p>
          </div>
        )}

        {loginMutation.isError && <p className="field-error">Sign-in failed. Try again.</p>}
      </div>
    </div>
  );
}
