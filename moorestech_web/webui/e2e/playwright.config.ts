import { defineConfig } from "@playwright/test";

const PORT = 5273;

// モックWSホスト相手に prod ビルドを配信して UI フローを検証する
// Serve the prod build against a mock WS host to verify UI flows
export default defineConfig({
  testDir: "./tests",
  timeout: 15_000,
  // mock host の currentBlock 等のグローバル状態を共有するため直列実行する
  // Run serially since specs share mock-host global state (e.g. currentBlock)
  fullyParallel: false,
  workers: 1,
  // 各機能の「動作する動画」を mp4 で残す（CEF 不要のブラウザ録画）
  // Keep a working-feature mp4 per spec (browser recording, no CEF needed)
  use: {
    baseURL: `http://127.0.0.1:${PORT}`,
    video: "on",
    trace: "retain-on-failure",
  },
  webServer: {
    command: "pnpm build && pnpm tsx e2e/mock-host/server.ts",
    cwd: "..",
    port: PORT,
    reuseExistingServer: false,
    timeout: 120_000,
    env: { MOCK_PORT: String(PORT) },
  },
});
