import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import { fileURLToPath, URL } from "node:url";

// backend は Unity が env で実ポートを注入。vite 側は Unity 起動時は CLI --port が優先され、この値は単体 `pnpm dev` 用フォールバック
// Unity injects the actual backend port via env; the vite port here is a standalone `pnpm dev` fallback (Unity passes CLI --port, which wins)
const vitePort = Number(process.env.MOORESTECH_VITE_PORT ?? 25173);
const backendPort = Number(process.env.MOORESTECH_BACKEND_PORT ?? 25050);

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
    port: vitePort,
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
        target: `http://127.0.0.1:${backendPort}`,
        changeOrigin: false,
      },
      "/ws": {
        target: `ws://127.0.0.1:${backendPort}`,
        ws: true,
        changeOrigin: false,
      },
    },
  },
});
