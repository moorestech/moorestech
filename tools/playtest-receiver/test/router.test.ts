import { env } from "cloudflare:test";
import { describe, expect, it } from "vitest";
import { handle } from "../src/index";
import type { Env } from "../src/env";

const noNetwork: typeof fetch = (async () => {
  throw new Error("テストからネットワークへ出てはいけない / tests must not reach the network");
}) as unknown as typeof fetch;

describe("router", () => {
  it("知らないパスはJSONの404を返す", async () => {
    const response = await handle(new Request("https://playtest.moores.tech/nope"), env as unknown as Env, noNetwork);
    expect(response.status).toBe(404);
    expect(await response.json()).toEqual({ error: "not_found" });
  });

  it("v1配下の知らないパスも同じ404ボディを返す", async () => {
    const response = await handle(new Request("https://playtest.moores.tech/v1/nope"), env as unknown as Env, noNetwork);
    expect(response.status).toBe(404);
    expect(await response.json()).toEqual({ error: "not_found" });
  });

  it("POST /v1/session はpostSessionへルーティングされる（bodyなしはbad-request）", async () => {
    // ルーティングの確認のみが目的。認証成功系のケースはsession.test.tsが担う
    // This only checks routing; the auth-success paths are covered by session.test.ts
    const request = new Request("https://playtest.moores.tech/v1/session", { method: "POST" });
    const response = await handle(request, env as unknown as Env, noNetwork);
    expect(response.status).toBe(400);
    expect(await response.json()).toEqual({ reason: "bad-request" });
  });

  it("知っているパスでもメソッドが違えば405を返す", async () => {
    const response = await handle(new Request("https://playtest.moores.tech/v1/session"), env as unknown as Env, noNetwork);
    expect(response.status).toBe(405);
    // 既知パスのエラーはreason形を保つ（R1逐語の404は未知パス専用）
    // Errors on a known path keep the reason shape; R1's verbatim 404 belongs to unknown paths only
    expect(await response.json()).toEqual({ reason: "method-not-allowed" });
  });

  it("許可リストの管理経路は404になる", async () => {
    // 撤去済みの管理経路。ルーティングの確認だけが目的で、実ネットワークには出ない（C14）
    // The admin route was removed; this checks routing only and never reaches the real network (C14)
    const ADMIN = (env as unknown as Env).ADMIN_KEY;
    const response = await handle(
      new Request("https://playtest.moores.tech/v1/allowlist", { headers: { "X-Admin-Key": ADMIN } }),
      env as unknown as Env,
      noNetwork,
    );
    expect(response.status).toBe(404);
    expect(await response.json()).toEqual({ error: "not_found" });
  });
});
