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
  // 必要なテストだけ明示して録画する: MOORESTECH_E2E_RECORD=1 pnpm exec playwright test --config e2e/playwright.config.ts e2e/tests/progress.spec.ts
  // Set the opt-in flag for one selected spec, not the whole suite
  use: {
    baseURL: `http://127.0.0.1:${PORT}`,
    video: process.env.MOORESTECH_E2E_RECORD === "1" ? "on" : "off",
    trace: "retain-on-failure",
  },
  webServer: {
    command: "pnpm build && node --import tsx e2e/mock-host/server.ts",
    cwd: "..",
    port: PORT,
    reuseExistingServer: false,
    timeout: 120_000,
    env: { MOCK_PORT: String(PORT), MOORESTECH_E2E: "true" },
  },
});
