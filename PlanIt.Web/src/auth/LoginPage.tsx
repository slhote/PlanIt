import { useState, type FormEvent } from "react";
import { useMutation } from "@tanstack/react-query";
import { Link, useNavigate } from "react-router";
import { login } from "../api/auth";
import { setSession } from "./authStore";

export function LoginPage() {
  const navigate = useNavigate();
  const [usernameOrEmail, setUsernameOrEmail] = useState("");
  const [password, setPassword] = useState("");

  const loginMutation = useMutation({
    mutationFn: () => login(usernameOrEmail, password),
    onSuccess: (result) => {
      setSession(result.user, result.accessToken, result.expiresInSeconds, result.refreshToken);
      navigate("/", { replace: true });
    },
  });

  function handleSubmit(e: FormEvent) {
    e.preventDefault();
    if (!usernameOrEmail.trim() || !password) return;
    loginMutation.mutate();
  }

  return (
    <div className="center-page">
      <div style={{ width: "100%", maxWidth: 380 }}>
        <div className="stack" style={{ marginBottom: "var(--space-6)", textAlign: "center" }}>
          <h1 style={{ fontSize: "var(--font-size-lg)" }}>PlanIt</h1>
          <p className="muted">Sign in to your account.</p>
        </div>

        <form onSubmit={handleSubmit}>
          <div className="field">
            <label htmlFor="login-username">Username or email</label>
            <input
              id="login-username"
              className="input"
              value={usernameOrEmail}
              onChange={(e) => setUsernameOrEmail(e.target.value)}
              autoFocus
              autoComplete="username"
              required
            />
          </div>
          <div className="field">
            <label htmlFor="login-password">Password</label>
            <input
              id="login-password"
              type="password"
              className="input"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              autoComplete="current-password"
              required
            />
          </div>

          {loginMutation.isError && <p className="field-error">Invalid username/email or password.</p>}

          <button
            type="submit"
            className="btn btn-primary btn-block"
            disabled={loginMutation.isPending || !usernameOrEmail.trim() || !password}
          >
            {loginMutation.isPending ? "Signing in…" : "Sign in"}
          </button>
        </form>

        <p className="muted" style={{ textAlign: "center", marginTop: "var(--space-4)" }}>
          No account yet? <Link to="/register">Create one</Link>
        </p>
      </div>
    </div>
  );
}
