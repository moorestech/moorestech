import { env } from "cloudflare:test";
import { describe, expect, it } from "vitest";
import { authenticateUserTicket, STEAM_IDENTITY } from "../src/steamAuth";
import type { Env } from "../src/env";

const workerEnv = env as unknown as Env;

function respond(body: unknown): typeof fetch {
  return (async () => new Response(JSON.stringify(body), { status: 200 })) as unknown as typeof fetch;
}

describe("authenticateUserTicket", () => {
  it("OK応答からsteamIdを取り出す", async () => {
    const result = await authenticateUserTicket(
      respond({ response: { params: { result: "OK", steamid: "76561198000000001", ownersteamid: "76561198000000001" } } }),
      workerEnv,
      "aabb",
    );
    expect(result.steamId).toBe("76561198000000001");
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

  it("error応答は失敗として理由を返す", async () => {
    const result = await authenticateUserTicket(
      respond({ response: { error: { errorcode: 101, errordesc: "Invalid ticket" } } }),
      workerEnv,
      "aabb",
    );
    expect(result.steamId).toBeNull();
    expect(result.reason).toContain("101");
  });

  it("resultがOK以外なら失敗", async () => {
    const result = await authenticateUserTicket(
      respond({ response: { params: { result: "Expired", steamid: "76561198000000001" } } }),
      workerEnv,
      "aabb",
    );
    expect(result.steamId).toBeNull();
  });

  it("Steam Web APIが落ちていれば失敗として畳む", async () => {
    const broken = (async () => {
      throw new Error("connection reset");
    }) as unknown as typeof fetch;
    const result = await authenticateUserTicket(broken, workerEnv, "aabb");
    expect(result.steamId).toBeNull();
    expect(result.reason).toContain("connection reset");
  });
});
