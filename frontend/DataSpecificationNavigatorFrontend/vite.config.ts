import path from "path"
import tailwindcss from "@tailwindcss/vite"
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
console.log("===== vite.config.ts =====");
console.log("process.env.VITE_BASE_URL:", process.env.VITE_BASE_URL);
console.log("process.env.VITE_BACKEND_API_URL:", process.env.VITE_BACKEND_API_URL);
export default defineConfig({
  base: process.env.VITE_BASE_URL || "/",
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      "@": path.resolve(__dirname, "./src"),
    },
  },
})
