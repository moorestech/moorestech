import { describe, expect, it, vi } from "vitest";
import { handle } from "../../src/index";
import { STEAM_ID, noNetwork, workerEnv } from "../support/uploadsFixture";

// 管理APIの認証（routeAdminの入口1箇所）のテスト。個々のエンドポイントはadmin/inbox.test.ts等が担う
// Tests for admin-API authentication (the single gate in routeAdmin); each endpoint is tested elsewhere
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

  // 認証は経路の形やメソッドより前で行われる。鍵なしなら不正kind・不正メソッド等どの形でも必ず401になる
  // Authentication runs before any path/method check; without a key, every path shape must always be 401
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
