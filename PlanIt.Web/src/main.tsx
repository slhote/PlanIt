import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { BrowserRouter } from "react-router";
import "./styles/theme.css";
import App from "./App.tsx";
import { configureRefresh } from "./auth/authStore";
import { refresh } from "./api/auth";

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

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <App />
      </BrowserRouter>
    </QueryClientProvider>
  </StrictMode>,
);
