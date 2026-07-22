import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// 静的ビルドのみ使用、開発サーバーは server.mjs が担当
// Build-only usage; server.mjs handles serving in production
export default defineConfig({
  plugins: [react()],
  base: "./",
});
