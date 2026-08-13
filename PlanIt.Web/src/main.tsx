import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { BrowserRouter } from "react-router";
import "./styles/theme.css";
import App from "./App.tsx";
import { configureRefresh } from "./auth/authStore";
import { refresh } from "./api/auth";

// Decode path restored by public/404.html (rafgraph/spa-github-pages pattern). GitHub Pages
// serves 404.html for unknown paths; that script re-encodes the path as a query string
// (?/project/123) and redirects to index.html. This block restores the original path before
// React Router mounts so it sees the real URL, not the redirect artifact.
const spaRedirect = window.location.search;
if (spaRedirect.startsWith("?/")) {
  const restored =
    import.meta.env.BASE_URL.replace(/\/$/, "") +
    spaRedirect.slice(1).replace(/~and~/g, "&") +
    window.location.hash;
  window.history.replaceState(null, "", restored);
}

// Wired once here, not inside authStore.ts itself, so that module stays free of a dependency
// on the API layer (api/httpClient.ts already imports from authStore.ts — see authStore.ts's
// own comment on why that would otherwise be circular).
configureRefresh(refresh);

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: 1,
      refetchOnWindowFocus: false,
    },
  },
});

// basename tells React Router to strip the Vite base path (/PlanIt/ in production, / in dev)
// before matching routes, so route definitions don't need to include the GitHub Pages subpath.
createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <BrowserRouter basename={import.meta.env.BASE_URL}>
        <App />
      </BrowserRouter>
    </QueryClientProvider>
  </StrictMode>,
);
