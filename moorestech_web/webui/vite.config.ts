import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// Vite dev server の設定
// Vite dev server configuration
export default defineConfig({
  plugins: [react()],
  server: {
    host: "127.0.0.1",
    port: 5173,
    strictPort: true,
    fs: {
      // リポジトリ外や node_modules 階層への /@fs/ アクセスを封じる
      // Prevent /@fs/ access to outside-repo / node_modules paths
      allow: ["./src", "./public", "./index.html"],
      strict: true,
    },
    proxy: {
      // Kestrel への HTTP + WebSocket プロキシ
      // Proxy HTTP and WebSocket to Kestrel
      "/api": {
        target: "http://127.0.0.1:5050",
        changeOrigin: false,
      },
      "/ws": {
        target: "ws://127.0.0.1:5050",
        ws: true,
        changeOrigin: false,
      },
    },
  },
});
