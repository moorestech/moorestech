import { env } from "cloudflare:test";
import { describe, expect, it } from "vitest";
import { STEAM_IDENTITY } from "../src/contract";
import { authenticateUserTicket } from "../src/steamAuth";
import type { Env } from "../src/env";

const workerEnv = env as unknown as Env;

function respond(body: unknown, status = 200): typeof fetch {
  return (async () => new Response(JSON.stringify(body), { status })) as unknown as typeof fetch;
}

describe("authenticateUserTicket", () => {
  it("OK応答からsteamIdを取り出す", async () => {
    const result = await authenticateUserTicket(
      respond({ response: { params: { result: "OK", steamid: "76561198000000001", ownersteamid: "76561198000000001" } } }),
      workerEnv,
      "aabb",
    );
    expect(result).toEqual({ kind: "verified", steamId: "76561198000000001" });
  });

  it("appid・identity・ticketをクエリに載せる", async () => {
    let seen = "";
    const spy = (async (input: RequestInfo) => {
      seen = typeof input === "string" ? input : (input as Request).url;
      return new Response(JSON.stringify({ response: { params: { result: "OK", steamid: "1" } } }), { status: 200 });
    }) as unknown as typeof fetch;
    await authenticateUserTicket(spy, workerEnv, "aabb");
    expect(seen).toContain("appid=1958160");
    expect(seen).toContain(`identity=${STEAM_IDENTITY}`);
    expect(seen).toContain("ticket=aabb");
  });

  it("error応答はrejectedとして理由を返す", async () => {
    const result = await authenticateUserTicket(
      respond({ response: { error: { errorcode: 101, errordesc: "Invalid ticket" } } }),
      workerEnv,
      "aabb",
    );
    expect(result.kind).toBe("rejected");
    expect(result.kind !== "verified" && result.reason).toContain("101");
  });

  it("resultがOK以外ならrejected", async () => {
    const result = await authenticateUserTicket(
      respond({ response: { params: { result: "Expired", steamid: "76561198000000001" } } }),
      workerEnv,
      "aabb",
    );
    expect(result).toEqual({ kind: "rejected", reason: "result-Expired" });
  });

  it("HTTP 5xxはunverifiable", async () => {
    const result = await authenticateUserTicket(respond({}, 503), workerEnv, "aabb");
    expect(result).toEqual({ kind: "unverifiable", reason: "http-503" });
  });

  it("paramsもerrorも無い応答はunverifiable", async () => {
    const result = await authenticateUserTicket(respond({ response: {} }), workerEnv, "aabb");
    expect(result).toEqual({ kind: "unverifiable", reason: "malformed-response" });
  });

  it("Steam Web APIが落ちていればunverifiableとして畳む", async () => {
    const broken = (async () => {
      throw new Error("connection reset");
    }) as unknown as typeof fetch;
    const result = await authenticateUserTicket(broken, workerEnv, "aabb");
    expect(result.kind).toBe("unverifiable");
    expect(result.kind !== "verified" && result.reason).toContain("connection reset");
  });

  it("fetch例外のメッセージにpublisher keyが含まれていればログ・reasonから伏せる（レビューMinor 2）", async () => {
    const leaking = (async () => {
      throw new Error(`fetch failed: https://partner.steam-api.com/...?key=${workerEnv.STEAM_WEB_API_KEY}&appid=1`);
    }) as unknown as typeof fetch;
    const result = await authenticateUserTicket(leaking, workerEnv, "aabb");
    if (result.kind === "verified") throw new Error("expected a failure");
    expect(result.reason).not.toContain(workerEnv.STEAM_WEB_API_KEY);
    expect(result.reason).toContain("***");
  });
});
