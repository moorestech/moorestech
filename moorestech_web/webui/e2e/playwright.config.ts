import { defineConfig } from "@playwright/test";

const PORT = 5273;

// モックWSホスト相手に prod ビルドを配信して UI フローを検証する
// Serve the prod build against a mock WS host to verify UI flows
export default defineConfig({
  testDir: "./tests",
  timeout: 15_000,
  use: { baseURL: `http://127.0.0.1:${PORT}` },
  webServer: {
    command: "pnpm build && pnpm tsx e2e/mock-host/server.ts",
    cwd: "..",
    port: PORT,
    reuseExistingServer: false,
    timeout: 120_000,
    env: { MOCK_PORT: String(PORT) },
  },
});
