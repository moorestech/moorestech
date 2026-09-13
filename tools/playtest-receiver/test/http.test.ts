import { describe, expect, it } from "vitest";
import { requireAdmin } from "../src/http";
import type { Env } from "../src/env";

// ADMIN_KEY未設定時のfail-openを塞いだ回帰テスト（レビュー Important 1）
// Regression test for the ADMIN_KEY-unset fail-open closed by review Important 1
function envWithAdminKey(adminKey: string): Env {
  return {
    BUCKET: {} as Env["BUCKET"],
    STEAM_APP_ID: "1958160",
    STEAM_WEB_API_KEY: "test-steam-key",
    SESSION_HMAC_SECRET: "test-hmac-secret",
    ADMIN_KEY: adminKey,
  };
}

describe("requireAdmin", () => {
  it("ADMIN_KEYが未設定なら、ヘッダの有無に関わらず401を返す", async () => {
    const env = envWithAdminKey("");
    const withoutHeader = requireAdmin(new Request("https://playtest.tar-atari.com/v1/admin"), env);
    expect(withoutHeader?.status).toBe(401);

    const withHeader = requireAdmin(
      new Request("https://playtest.tar-atari.com/v1/admin", { headers: { "x-admin-key": "" } }),
      env,
    );
    expect(withHeader?.status).toBe(401);
  });

  it("ADMIN_KEYが設定されていて一致すれば通過する", () => {
    const env = envWithAdminKey("secret-admin-key");
    const response = requireAdmin(
      new Request("https://playtest.tar-atari.com/v1/admin", { headers: { "x-admin-key": "secret-admin-key" } }),
      env,
    );
    expect(response).toBeNull();
  });

  it("ADMIN_KEYが設定されていて不一致なら401を返す", () => {
    const env = envWithAdminKey("secret-admin-key");
    const response = requireAdmin(
      new Request("https://playtest.tar-atari.com/v1/admin", { headers: { "x-admin-key": "wrong-key" } }),
      env,
    );
    expect(response?.status).toBe(401);
  });
});
