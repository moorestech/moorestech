import { cloudflareTest } from "@cloudflare/vitest-pool-workers";
import { defineConfig } from "vitest/config";

// 実物のR2（miniflare）でテストする。R2の偽物を書くと索引とマーカーの整合を守れない
// Tests run against a real (miniflare) R2; a hand-written fake would not keep markers and the index consistent
export default defineConfig({
  plugins: [
    cloudflareTest({
      wrangler: { configPath: "./wrangler.toml" },
      miniflare: {
        bindings: {
          STEAM_WEB_API_KEY: "test-steam-key",
          SESSION_HMAC_SECRET: "test-hmac-secret",
          ADMIN_KEY: "test-admin-key",
          R2_ACCESS_KEY_ID: "test-access-key-id",
          R2_SECRET_ACCESS_KEY: "test-secret-access-key",
        },
      },
    }),
  ],
  test: {
    include: ["test/**/*.test.ts"],
  },
});
