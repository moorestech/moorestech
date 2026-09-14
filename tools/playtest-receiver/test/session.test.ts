import { env } from "cloudflare:test";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { handle } from "../src/index";
import { ALLOWLIST_KEY, writeAllowlist } from "../src/allowlist";
import { verifyToken } from "../src/token";
import type { Env } from "../src/env";

const workerEnv = env as unknown as Env;

function steamOk(steamId: string): typeof fetch {
  return (async () =>
    new Response(JSON.stringify({ response: { params: { result: "OK", steamid: steamId } } }), { status: 200 })) as unknown as typeof fetch;
}
const steamNg: typeof fetch = (async () =>
  new Response(JSON.stringify({ response: { error: { errorcode: 101, errordesc: "Invalid ticket" } } }), { status: 200 })) as unknown as typeof fetch;

function sessionRequest(body: string): Request {
  return new Request("https://playtest.tar-atari.com/v1/session", { method: "POST", body });
}

describe("POST /v1/session", () => {
  beforeEach(async () => {
    await workerEnv.BUCKET.delete(ALLOWLIST_KEY);
  });

  it("許可されたSteamIDへトークンを発行する", async () => {
    await writeAllowlist(workerEnv.BUCKET, ["76561198000000001"]);
    const response = await handle(sessionRequest(JSON.stringify({ ticket: "aabb" })), workerEnv, steamOk("76561198000000001"));
    expect(response.status).toBe(200);
    const body = (await response.json()) as { steamId: string; allowed: boolean; token: string };
    expect(body.steamId).toBe("76561198000000001");
    expect(body.allowed).toBe(true);
    expect(await verifyToken(workerEnv.SESSION_HMAC_SECRET, body.token, Math.floor(Date.now() / 1000))).toBe("76561198000000001");
  });

  it("許可リストに無ければ403", async () => {
    await writeAllowlist(workerEnv.BUCKET, ["76561198000000002"]);
    const response = await handle(sessionRequest(JSON.stringify({ ticket: "aabb" })), workerEnv, steamOk("76561198000000001"));
    expect(response.status).toBe(403);
    expect(await response.json()).toEqual({ reason: "not-allowed" });
  });

  it("許可リストが空でも誰も通さない", async () => {
    const response = await handle(sessionRequest(JSON.stringify({ ticket: "aabb" })), workerEnv, steamOk("76561198000000001"));
    expect(response.status).toBe(403);
  });

  it("チケット検証に失敗すれば401", async () => {
    const response = await handle(sessionRequest(JSON.stringify({ ticket: "aabb" })), workerEnv, steamNg);
    expect(response.status).toBe(401);
    expect(await response.json()).toEqual({ reason: "invalid-ticket" });
  });

  it("ticketが16進でなければ400", async () => {
    const response = await handle(sessionRequest(JSON.stringify({ ticket: "zz" })), workerEnv, steamOk("1"));
    expect(response.status).toBe(400);
    expect(await response.json()).toEqual({ reason: "bad-request" });
  });

  it("ticketが奇数長の16進なら400（バイナリチケットは必ず偶数長）", async () => {
    const response = await handle(sessionRequest(JSON.stringify({ ticket: "abc" })), workerEnv, steamOk("1"));
    expect(response.status).toBe(400);
    expect(await response.json()).toEqual({ reason: "bad-request" });
  });

  it("bodyがJSONでなければ400", async () => {
    const response = await handle(sessionRequest("not json"), workerEnv, steamOk("1"));
    expect(response.status).toBe(400);
  });

  it("bodyがJSONのnullなら400（tryの外でbody.ticketに触るとTypeErrorになる回帰の再発防止）", async () => {
    const response = await handle(sessionRequest("null"), workerEnv, steamOk("1"));
    expect(response.status).toBe(400);
    expect(await response.json()).toEqual({ reason: "bad-request" });
  });

  describe("400経路のwarnログ（レビューImportant 1: 無音の縮退禁止）", () => {
    afterEach(() => {
      vi.restoreAllMocks();
    });

    it("bodyが非JSONなら理由付きでwarnする", async () => {
      const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
      await handle(sessionRequest("not json"), workerEnv, steamOk("1"));
      expect(warn).toHaveBeenCalledWith(expect.stringContaining("[session] rejected: body is not JSON"));
    });

    it("ticketが16進形式でなければ理由付きでwarnする", async () => {
      const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
      await handle(sessionRequest(JSON.stringify({ ticket: "zz" })), workerEnv, steamOk("1"));
      expect(warn).toHaveBeenCalledWith("[session] rejected: ticket is not an even-length hex string");
    });

    it("body非JSONのときhex側の文言は出ない", async () => {
      const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
      await handle(sessionRequest("not json"), workerEnv, steamOk("1"));
      const jsonReasonCalls = warn.mock.calls.filter((call) => String(call[0]).includes("is not JSON"));
      const hexReasonCalls = warn.mock.calls.filter((call) => String(call[0]).includes("is not an even-length hex string"));
      expect(jsonReasonCalls.length).toBe(1);
      expect(hexReasonCalls.length).toBe(0);
    });

    it("ticketがhex不正のときJSON側の文言は出ない", async () => {
      const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
      await handle(sessionRequest(JSON.stringify({ ticket: "zz" })), workerEnv, steamOk("1"));
      const jsonReasonCalls = warn.mock.calls.filter((call) => String(call[0]).includes("is not JSON"));
      const hexReasonCalls = warn.mock.calls.filter((call) => String(call[0]).includes("is not an even-length hex string"));
      expect(jsonReasonCalls.length).toBe(0);
      expect(hexReasonCalls.length).toBe(1);
    });
  });

  it("SESSION_HMAC_SECRETが空なら500 server-misconfiguredを返しtoken:nullを漏らさない", async () => {
    // Task 1の変更でsignTokenはnullを返せるようになった。/v1/sessionはその場合200を返してはいけない
    // Task 1 made signToken able to return null; /v1/session must not answer 200 in that case
    await writeAllowlist(workerEnv.BUCKET, ["76561198000000001"]);
    const envWithoutSecret: Env = { ...workerEnv, SESSION_HMAC_SECRET: "" };
    const response = await handle(sessionRequest(JSON.stringify({ ticket: "aabb" })), envWithoutSecret, steamOk("76561198000000001"));
    expect(response.status).toBe(500);
    expect(await response.json()).toEqual({ reason: "server-misconfigured" });
  });
});
