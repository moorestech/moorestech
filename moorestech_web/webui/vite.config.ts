import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import { fileURLToPath, URL } from "node:url";

// Vite dev server の設定
// Vite dev server configuration
export default defineConfig({
  plugins: [react()],
  resolve: {
    // tsconfig の @/ paths を vite 側にも対応させる
    // Mirror the tsconfig @/ paths on the vite side
    alias: { "@": fileURLToPath(new URL("./src", import.meta.url)) },
  },
  server: {
    host: "127.0.0.1",
    port: 5173,
    strictPort: true,
    fs: {
      // Vite は allow リストに node_modules を含むエントリを拒否するため、
      // プロジェクトルート自体を許可してデフォルト挙動(リポジトリ外は元々アクセス不可)に委ねる
      // Vite rejects allow entries containing node_modules, so allow the project root itself
      // and rely on the default behavior (access outside the repo is already blocked)
      allow: ["."],
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
