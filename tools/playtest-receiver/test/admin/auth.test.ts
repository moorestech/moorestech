import { describe, expect, it, vi } from "vitest";
import { handle } from "../../src/index";
import { STEAM_ID, noNetwork, workerEnv } from "../support/uploadsFixture";

// 管理APIの認証（routeAdminの入口1箇所）に関するテスト。個々のエンドポイントの中身は
// admin/inbox.test.ts・admin/allowlist.test.ts が担う（200行規約での分割）
// Tests for admin-API authentication (the single gate in routeAdmin); each endpoint's own
// behavior lives in admin/inbox.test.ts and admin/allowlist.test.ts (split for the 200-line rule)
describe("admin api auth", () => {
  it("adminキーが無ければ401でwarnする", async () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    const response = await handle(new Request("https://playtest.tar-atari.com/v1/inbox"), workerEnv, noNetwork);
    expect(response.status).toBe(401);
    expect(await response.json()).toEqual({ reason: "unauthorized" });
    expect(warn).toHaveBeenCalled();
    warn.mockRestore();
  });

  it("adminキーが違えば401でwarnする", async () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    const response = await handle(
      new Request("https://playtest.tar-atari.com/v1/inbox", { headers: { "x-admin-key": "wrong" } }),
      workerEnv,
      noNetwork,
    );
    expect(response.status).toBe(401);
    expect(warn).toHaveBeenCalled();
    warn.mockRestore();
  });

  // 認証は経路の形やメソッドより前で行われる。鍵なしでは、不正kind・不正メソッド等いずれの経路形でも
  // 405/400/404を漏らさず必ず401になることを確認する
  // Authentication runs before any path-shape or method check; without a key, no path shape (bad kind,
  // wrong method) should leak a 405/400/404 — it must always be 401
  it("adminキーが無ければ経路の形に関わらず401になる（不正kindの inbox パス）", async () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    const response = await handle(
      new Request(`https://playtest.tar-atari.com/v1/inbox/badkind/${STEAM_ID}/20260913_120000_aaaa1111/ack`, { method: "POST" }),
      workerEnv,
      noNetwork,
    );
    expect(response.status).toBe(401);
    expect(await response.json()).toEqual({ reason: "unauthorized" });
    expect(warn).toHaveBeenCalled();
    warn.mockRestore();
  });

  it("adminキーが無ければ経路の形に関わらず401になる（allowlistへの不正メソッド）", async () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    const response = await handle(new Request("https://playtest.tar-atari.com/v1/allowlist", { method: "POST" }), workerEnv, noNetwork);
    expect(response.status).toBe(401);
    expect(warn).toHaveBeenCalled();
    warn.mockRestore();
  });
});
