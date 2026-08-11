import { useEffect, useState } from "react";
import { AppRoutes } from "./router/AppRoutes";
import { getRememberedUserId, setSession } from "./auth/authStore";
import { fetchUser } from "./api/users";
import { mockLogin } from "./api/auth";

function App() {
  const [bootstrapped, setBootstrapped] = useState(false);

  useEffect(() => {
    const rememberedUserId = getRememberedUserId();
    if (!rememberedUserId) {
      setBootstrapped(true);
      return;
    }
    fetchUser(rememberedUserId)
      .then(async (user) => {
        if (user) {
          const result = await mockLogin(user);
          setSession(result.user, result.accessToken, result.expiresInSeconds);
        }
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
