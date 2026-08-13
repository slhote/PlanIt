import { Link, Outlet, useNavigate } from "react-router";
import { useAuth } from "../auth/useAuth";
import { clearSession, getRefreshToken } from "../auth/authStore";
import { logout } from "../api/auth";
import { initials } from "./initials";

export function AppShell() {
  const { user } = useAuth();
  const navigate = useNavigate();

  async function handleLogout() {
    const refreshToken = getRefreshToken();
    if (refreshToken) {
      // Best-effort — revoke server-side, but don't block the user from leaving if it fails
      // (e.g. token already expired). The local session is cleared either way.
      await logout(refreshToken).catch(() => {});
    }
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
