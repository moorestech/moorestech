import { env } from "cloudflare:test";
import { describe, expect, it } from "vitest";
import { handle } from "../src/index";
import type { Env } from "../src/env";

const noNetwork: typeof fetch = (async () => {
  throw new Error("テストからネットワークへ出てはいけない / tests must not reach the network");
}) as unknown as typeof fetch;

describe("router", () => {
  it("知らないパスはJSONの404を返す", async () => {
    const response = await handle(new Request("https://playtest.tar-atari.com/nope"), env as unknown as Env, noNetwork);
    expect(response.status).toBe(404);
    expect(await response.json()).toEqual({ reason: "not-found" });
  });

  it("知っているパスでもメソッドが違えば405を返す", async () => {
    const response = await handle(new Request("https://playtest.tar-atari.com/v1/session"), env as unknown as Env, noNetwork);
    expect(response.status).toBe(405);
  });
});
