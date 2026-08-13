import { useEffect, useRef, useState } from "react";
import { AppRoutes } from "./router/AppRoutes";
import { getStoredRefreshToken, setSession } from "./auth/authStore";
import { refresh } from "./api/auth";

function App() {
  const [bootstrapped, setBootstrapped] = useState(false);
  // StrictMode (main.tsx) double-invokes effects in development — without this guard, the
  // bootstrap effect fires refresh() twice with the same stored token. The backend's refresh
  // rotation treats a token presented a second time as reuse (a real client can't legitimately
  // call /auth/refresh twice with the same token) and revokes every active session for the user,
  // logging them out. A ref survives the StrictMode remount-on-the-same-instance, unlike a
  // module-level flag which would also (harmlessly) survive but be unnecessarily global.
  const bootstrapStarted = useRef(false);

  useEffect(() => {
    if (bootstrapStarted.current) return;
    bootstrapStarted.current = true;

    const storedRefreshToken = getStoredRefreshToken();
    if (!storedRefreshToken) {
      setBootstrapped(true);
      return;
    }
    refresh(storedRefreshToken)
      .then((result) => {
        setSession(result.user, result.accessToken, result.expiresInSeconds, result.refreshToken);
      })
      .catch(() => {
        // Stored refresh token is expired/revoked/reused — fall through to /login.
      })
      .finally(() => setBootstrapped(true));
  }, []);

  if (!bootstrapped) {
    return (
      <div className="center-page">
        <div className="spinner" />
      </div>
    );
  }

  return <AppRoutes />;
}

export default App;
