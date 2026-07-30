import { defineConfig } from "@playwright/test";

const VITE_PORT = 5274;
const MOCK_PORT = 5275;

// 開発サーバー経由で開発専用UIの回帰を検証する
// Verify development-only UI regressions through the development server
export default defineConfig({
  testDir: "../tests",
  timeout: 15_000,
  // mock hostの状態を共有するため直列実行する
  // Run serially because the mock host state is shared
  fullyParallel: false,
  workers: 1,
  use: {
    baseURL: `http://127.0.0.1:${VITE_PORT}`,
    video: "on",
    trace: "retain-on-failure",
  },
  webServer: {
    command: `sh -c 'MOCK_PORT=${MOCK_PORT} node --import tsx e2e/mock-host/server.ts & mock_pid=$!; trap "kill $mock_pid" EXIT INT TERM; MOORESTECH_VITE_PORT=${VITE_PORT} MOORESTECH_BACKEND_PORT=${MOCK_PORT} pnpm dev'`,
    cwd: "../..",
    port: VITE_PORT,
    reuseExistingServer: false,
    timeout: 120_000,
  },
});
