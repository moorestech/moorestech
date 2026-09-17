import { beforeEach, describe, expect, it, vi } from "vitest";
import { ALLOWLIST_KEY } from "../../src/allowlist";
import { handle } from "../../src/index";
import { STEAM_ID, clean, noNetwork, workerEnv } from "../support/uploadsFixture";

const ADMIN = { "x-admin-key": "test-admin-key" };
const URL_ALLOWLIST = "https://playtest.tar-atari.com/v1/allowlist";

function put(body: string): Promise<Response> {
  return handle(new Request(URL_ALLOWLIST, { method: "PUT", headers: ADMIN, body }), workerEnv, noNetwork);
}

function get(): Promise<Response> {
  return handle(new Request(URL_ALLOWLIST, { headers: ADMIN }), workerEnv, noNetwork);
}

// 認証の合否は admin/auth.test.ts が担う。ここでは admin キーが正しい前提の中身を検証する
// admin/auth.test.ts owns authentication pass/fail; this file assumes a valid admin key throughout
describe("admin api allowlist", () => {
  beforeEach(clean);

  it("許可リストがGET/PUTで往復する", async () => {
    expect((await put(JSON.stringify({ steamIds: [STEAM_ID] }))).status).toBe(200);
    expect(await (await get()).json()).toEqual({ steamIds: [STEAM_ID] });
  });

  it("PUTの応答は実際に保存した重複除去済みの配列を返す", async () => {
    const response = await put(JSON.stringify({ steamIds: [STEAM_ID, STEAM_ID] }));
    expect(await response.json()).toEqual({ steamIds: [STEAM_ID] });
  });

  it("許可リストPUTの本文が壊れていれば400でwarnし現状を壊さない", async () => {
    await put(JSON.stringify({ steamIds: [STEAM_ID] }));
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    const broken = await put("{ nope");
    expect(broken.status).toBe(400);
    expect(warn).toHaveBeenCalled();
    warn.mockRestore();
    expect(await (await get()).json()).toEqual({ steamIds: [STEAM_ID] });
  });

  it("許可リストPUTの本文がsteamIds配列でなければ400でwarnする", async () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    const response = await put(JSON.stringify({ steamIds: [1] }));
    expect(response.status).toBe(400);
    expect(warn).toHaveBeenCalled();
    warn.mockRestore();
  });

  // 保存済みが壊れていたらGETは503。空を返すとGET→編集→全置換PUTで許可リストを消し飛ばす（F24）
  // A corrupt stored list makes GET answer 503; returning [] would let GET -> edit -> full PUT wipe it (F24)
  it("保存済みの許可リストが壊れていればGETは503でwarnし、PUTの全置換で修復できる", async () => {
    await workerEnv.BUCKET.put(ALLOWLIST_KEY, "{ broken");
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    const corrupt = await get();
    expect(corrupt.status).toBe(503);
    expect(await corrupt.json()).toEqual({ reason: "allowlist-unavailable" });
    expect(warn).toHaveBeenCalled();
    warn.mockRestore();

    expect((await put(JSON.stringify({ steamIds: [STEAM_ID] }))).status).toBe(200);
    expect(await (await get()).json()).toEqual({ steamIds: [STEAM_ID] });
  });

  it("GET/PUT以外のメソッドは405でwarnする", async () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    const response = await handle(new Request(URL_ALLOWLIST, { method: "POST", headers: ADMIN }), workerEnv, noNetwork);
    expect(response.status).toBe(405);
    expect(await response.json()).toEqual({ reason: "method-not-allowed" });
    expect(warn).toHaveBeenCalled();
    warn.mockRestore();
  });
});
