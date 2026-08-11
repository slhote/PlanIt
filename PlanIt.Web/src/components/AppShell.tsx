import { useState } from "react";
import { Link, Outlet, useNavigate } from "react-router";
import { useAuth } from "../auth/useAuth";
import { clearSession } from "../auth/authStore";
import { isChaosMode, setChaosMode } from "../api/mockClient";
import { initials } from "./initials";

export function AppShell() {
  const { user } = useAuth();
  const navigate = useNavigate();
  const [chaos, setChaos] = useState(isChaosMode());

  function handleLogout() {
    clearSession();
    navigate("/login", { replace: true });
  }

  return (
    <div className="app-shell">
      <header className="topbar">
        <Link to="/" className="topbar-title" style={{ textDecoration: "none" }}>
          PlanIt
        </Link>

        {user && (
          <div className="row">
            <label
              className="row chaos-toggle"
              title="Forces the next save (drag, edit, delete, reorder…) to fail, so you can see error handling and optimistic-update revert."
            >
              <input
                type="checkbox"
                checked={chaos}
                onChange={(e) => {
                  setChaos(e.target.checked);
                  setChaosMode(e.target.checked);
                }}
              />
              <span className="muted">Simulate failure</span>
            </label>

            <span className="avatar avatar-sm" title={user.username}>
              {initials(user.username)}
            </span>
            <button type="button" className="icon-btn" title="Log out" aria-label="Log out" onClick={handleLogout}>
              <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4" />
                <polyline points="16 17 21 12 16 7" />
                <line x1="21" y1="12" x2="9" y2="12" />
              </svg>
            </button>
          </div>
        )}
      </header>
      <Outlet />
    </div>
  );
}
