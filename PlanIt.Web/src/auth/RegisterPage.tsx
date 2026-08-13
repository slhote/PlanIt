import { useState, type FormEvent } from "react";
import { useMutation } from "@tanstack/react-query";
import { Link, useNavigate } from "react-router";
import { register } from "../api/auth";
import { setSession } from "./authStore";

const MIN_PASSWORD_LENGTH = 8;

export function RegisterPage() {
  const navigate = useNavigate();
  const [username, setUsername] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");

  const registerMutation = useMutation({
    mutationFn: () => register(username.trim(), email.trim(), password),
    onSuccess: (result) => {
      setSession(result.user, result.accessToken, result.expiresInSeconds, result.refreshToken);
      navigate("/", { replace: true });
    },
  });

  const canSubmit = username.trim().length > 0 && email.trim().length > 0 && password.length >= MIN_PASSWORD_LENGTH;

  function handleSubmit(e: FormEvent) {
    e.preventDefault();
    if (!canSubmit) return;
    registerMutation.mutate();
  }

  return (
    <div className="center-page">
      <div style={{ width: "100%", maxWidth: 380 }}>
        <div className="stack" style={{ marginBottom: "var(--space-6)", textAlign: "center" }}>
          <h1 style={{ fontSize: "var(--font-size-lg)" }}>PlanIt</h1>
          <p className="muted">Create an account.</p>
        </div>

        <form onSubmit={handleSubmit}>
          <div className="field">
            <label htmlFor="register-username">Username</label>
            <input
              id="register-username"
              className="input"
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              autoFocus
              autoComplete="username"
              required
            />
          </div>
          <div className="field">
            <label htmlFor="register-email">Email</label>
            <input
              id="register-email"
              type="email"
              className="input"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              autoComplete="email"
              required
            />
          </div>
          <div className="field">
            <label htmlFor="register-password">Password</label>
            <input
              id="register-password"
              type="password"
              className="input"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              autoComplete="new-password"
              minLength={MIN_PASSWORD_LENGTH}
              required
            />
            <p className="muted" style={{ marginTop: "var(--space-1)" }}>
              At least {MIN_PASSWORD_LENGTH} characters.
            </p>
          </div>

          {registerMutation.isError && (
            <p className="field-error">
              Couldn't create that account — the username or email may already be taken.
            </p>
          )}

          <button type="submit" className="btn btn-primary btn-block" disabled={registerMutation.isPending || !canSubmit}>
            {registerMutation.isPending ? "Creating account…" : "Create account"}
          </button>
        </form>

        <p className="muted" style={{ textAlign: "center", marginTop: "var(--space-4)" }}>
          Already have an account? <Link to="/login">Sign in</Link>
        </p>
      </div>
    </div>
  );
}
