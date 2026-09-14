import { beforeEach, describe, expect, it, vi } from "vitest";
import { handle } from "../../src/index";
import { STEAM_ID, clean, noNetwork, workerEnv } from "../support/uploadsFixture";

const ADMIN = { "x-admin-key": "test-admin-key" };

// 認証の合否は admin/auth.test.ts が担う。ここでは admin キーが正しい前提の中身を検証する
// admin/auth.test.ts owns authentication pass/fail; this file assumes a valid admin key throughout
describe("admin api allowlist", () => {
  beforeEach(clean);

  it("許可リストがGET/PUTで往復する", async () => {
    const put = await handle(
      new Request("https://playtest.tar-atari.com/v1/allowlist", {
        method: "PUT",
        headers: ADMIN,
        body: JSON.stringify({ steamIds: [STEAM_ID] }),
      }),
      workerEnv,
      noNetwork,
    );
    expect(put.status).toBe(200);
    const get = await handle(new Request("https://playtest.tar-atari.com/v1/allowlist", { headers: ADMIN }), workerEnv, noNetwork);
    expect(await get.json()).toEqual({ steamIds: [STEAM_ID] });
  });

  it("許可リストPUTの本文が壊れていれば400でwarnし現状を壊さない", async () => {
    await handle(
      new Request("https://playtest.tar-atari.com/v1/allowlist", { method: "PUT", headers: ADMIN, body: JSON.stringify({ steamIds: [STEAM_ID] }) }),
      workerEnv,
      noNetwork,
    );
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    const broken = await handle(
      new Request("https://playtest.tar-atari.com/v1/allowlist", { method: "PUT", headers: ADMIN, body: "{ nope" }),
      workerEnv,
      noNetwork,
    );
    expect(broken.status).toBe(400);
    expect(warn).toHaveBeenCalled();
    warn.mockRestore();
    const get = await handle(new Request("https://playtest.tar-atari.com/v1/allowlist", { headers: ADMIN }), workerEnv, noNetwork);
    expect(await get.json()).toEqual({ steamIds: [STEAM_ID] });
  });

  it("許可リストPUTの本文がsteamIds配列でなければ400でwarnする", async () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    const response = await handle(
      new Request("https://playtest.tar-atari.com/v1/allowlist", { method: "PUT", headers: ADMIN, body: JSON.stringify({ steamIds: [1] }) }),
      workerEnv,
      noNetwork,
    );
    expect(response.status).toBe(400);
    expect(warn).toHaveBeenCalled();
    warn.mockRestore();
  });
});
