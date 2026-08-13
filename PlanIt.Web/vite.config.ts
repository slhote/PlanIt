import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  // Sets the base URL for production builds on GitHub Pages (https://slhote.github.io/PlanIt/).
  // Vite exposes this as import.meta.env.BASE_URL at runtime — BrowserRouter uses it as its
  // basename so React Router strips the /PlanIt/ prefix before matching routes.
  base: '/PlanIt/',
})
