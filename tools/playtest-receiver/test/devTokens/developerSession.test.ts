import { beforeEach, expect, it } from "vitest";
import { verifyToken } from "../../src/token";
import { clean, declaration, handle, ID, noNetwork, putDirect, workerEnv } from "../support/uploadsFixture";
import { admin, exchange, issue, upload } from "../support/developerFixture";

beforeEach(clean);

it("発行・交換したセッションで開発機の報告を完成できる", async () => {
  const credential = await issue("macbook");
  const response = await exchange(JSON.stringify({ token: credential }));
  expect(response.status).toBe(200);
  const body = await response.json() as { steamId: string; allowed: boolean; token: string; expiresAt: string };
  expect(Object.keys(body).sort()).toEqual(["allowed", "expiresAt", "steamId", "token"]);
  expect(body.steamId).toBe("dev-macbook");
  expect(body.allowed).toBe(true);
  expect(await verifyToken(workerEnv.SESSION_HMAC_SECRET, body.token, Math.floor(Date.now() / 1000)))
    .toEqual({ kind: "ok", principal: { kind: "developer", subject: "dev-macbook", credentialHash: expect.stringMatching(/^[0-9a-f]{64}$/) } });
  expect(Date.parse(body.expiresAt)).toBeGreaterThan(Date.now());
  expect((await upload("prepare", "report", body.token, declaration({ "manifest.json": 2 }))).status).toBe(200);
  await putDirect("report", "dev-macbook", ID, "manifest.json", "{}");
  expect((await upload("complete", "report", body.token, "{}")).status).toBe(200);
  expect(await workerEnv.BUCKET.head(`reports/dev-macbook/${ID}/READY`)).not.toBeNull();
  expect((await admin("DELETE", "/macbook", "", workerEnv.ADMIN_KEY)).status).toBe(200);
  const refused = await exchange(JSON.stringify({ token: credential }));
  expect(refused.status).toBe(401);
  expect(await refused.json()).toEqual({ reason: "invalid-developer-token" });
});

it.each(["a".repeat(64), "mdt_" + "a".repeat(63), "mdt_" + "A".repeat(64)])("不正な形を400にする: %s", async token => {
  const response = await exchange(JSON.stringify({ token }));
  expect(response.status).toBe(400);
  expect(await response.json()).toEqual({ reason: "bad-request" });
});

it.each(["null", "{}", "not json"])("壊れた本文を400にする: %s", async body => {
  expect((await exchange(body)).status).toBe(400);
});

it("署名設定が欠けたら500", async () => {
  const token = await issue("macbook");
  const response = await handle(new Request("https://x/v1/session/developer", {
    method: "POST", body: JSON.stringify({ token }),
  }), { ...workerEnv, SESSION_HMAC_SECRET: "" }, noNetwork);
  expect(response.status).toBe(500);
  expect(await response.json()).toEqual({ reason: "server-misconfigured" });
});


it("同秒に同名再発行しても旧JWTはprepareとcompleteへ復活しない", async () => {
  const first = await issue("macbook");
  const old = await (await exchange(JSON.stringify({ token: first }))).json() as { token: string };
  expect((await upload("prepare", "report", old.token, declaration({ "manifest.json": 2 }))).status).toBe(200);
  await putDirect("report", "dev-macbook", ID, "manifest.json", "{}");
  expect((await admin("DELETE", "/macbook", "", workerEnv.ADMIN_KEY)).status).toBe(200);
  const second = await issue("macbook");
  const current = await (await exchange(JSON.stringify({ token: second }))).json() as { token: string };
  for (const action of ["prepare", "complete"] as const) {
    const body = action === "prepare" ? declaration({ "manifest.json": 2 }) : "{}";
    const refused = await upload(action, "report", old.token, body);
    expect(refused.status).toBe(401);
    expect(await refused.json()).toEqual({ reason: "developer-token-revoked" });
    expect((await upload(action, "report", current.token, body)).status).toBe(200);
  }
  expect((await exchange(JSON.stringify({ token: first }))).status).toBe(401);
});
