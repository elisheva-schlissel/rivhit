import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// The dev server proxies /api to the ASP.NET Core backend so the browser makes
// same-origin calls (no CORS in dev) and never talks to the time source directly.
export default defineConfig({
  root: import.meta.dirname,
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      "/api": {
        target: "http://localhost:5138",
        changeOrigin: true,
      },
    },
  },
});
