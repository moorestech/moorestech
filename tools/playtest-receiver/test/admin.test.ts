import { beforeEach, describe, expect, it, vi } from "vitest";
import { handle } from "../src/index";
import { signToken } from "../src/token";
import { clean, noNetwork, workerEnv } from "./support/uploadsFixture";

const STEAM_ID = "76561198000000001";
const ADMIN = { "x-admin-key": "test-admin-key" };

async function upload(kind: string, id: string, path: string, body: string): Promise<void> {
  const token = await signToken(workerEnv.SESSION_HMAC_SECRET, STEAM_ID, Math.floor(Date.now() / 1000));
  await handle(
    new Request(`https://playtest.tar-atari.com/v1/uploads/${kind}/${id}/${path}`, {
      method: "PUT",
      headers: { authorization: `Bearer ${token}`, "content-length": String(body.length) },
      body,
    }),
    workerEnv,
    noNetwork,
  );
  await handle(
    new Request(`https://playtest.tar-atari.com/v1/uploads/${kind}/${id}/complete`, {
      method: "POST",
      headers: { authorization: `Bearer ${token}` },
      body: JSON.stringify({ kind: "bug" }),
    }),
    workerEnv,
    noNetwork,
  );
}

describe("admin api", () => {
  beforeEach(clean);

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

  it("kindが不正なら400でwarnする", async () => {
    await upload("report", "20260913_120000_aaaa1111", "a.txt", "x");
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    const response = await handle(
      new Request(`https://playtest.tar-atari.com/v1/inbox/config/${STEAM_ID}/20260913_120000_aaaa1111/a.txt`, { headers: ADMIN }),
      workerEnv,
      noNetwork,
    );
    expect(response.status).toBe(400);
    expect(await response.json()).toEqual({ reason: "bad-kind" });
    expect(warn).toHaveBeenCalled();
    warn.mockRestore();
  });

  it("completeした2件がinboxに出る", async () => {
    await upload("report", "20260913_120000_aaaa1111", "a.txt", "x");
    await upload("progress", "20260913_130000_bbbb2222", "record.json", "{}");
    const response = await handle(new Request("https://playtest.tar-atari.com/v1/inbox", { headers: ADMIN }), workerEnv, noNetwork);
    const body = (await response.json()) as { items: { kind: string; steamId: string; id: string; readyAt: string }[]; cursor: string | null };
    expect(body.items).toHaveLength(2);
    expect(body.items.map((item) => item.id).sort()).toEqual(["20260913_120000_aaaa1111", "20260913_130000_bbbb2222"]);
    expect(body.items[0]?.steamId).toBe(STEAM_ID);
    expect(Number.isNaN(Date.parse(body.items[0]?.readyAt ?? ""))).toBe(false);
  });

  it("inboxの個別ファイルを取れる。不在は404でwarnする", async () => {
    await upload("report", "20260913_120000_aaaa1111", "a.txt", "hello");
    const ok = await handle(
      new Request(`https://playtest.tar-atari.com/v1/inbox/report/${STEAM_ID}/20260913_120000_aaaa1111/a.txt`, { headers: ADMIN }),
      workerEnv,
      noNetwork,
    );
    expect(await ok.text()).toBe("hello");

    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    const missing = await handle(
      new Request(`https://playtest.tar-atari.com/v1/inbox/report/${STEAM_ID}/20260913_120000_aaaa1111/none.txt`, { headers: ADMIN }),
      workerEnv,
      noNetwork,
    );
    expect(missing.status).toBe(404);
    expect(warn).toHaveBeenCalled();
    warn.mockRestore();
  });

  it("ackするとinboxから消えACKEDが出来る", async () => {
    await upload("report", "20260913_120000_aaaa1111", "a.txt", "x");
    const acked = await handle(
      new Request(`https://playtest.tar-atari.com/v1/inbox/report/${STEAM_ID}/20260913_120000_aaaa1111/ack`, { method: "POST", headers: ADMIN }),
      workerEnv,
      noNetwork,
    );
    expect(acked.status).toBe(200);
    expect(await workerEnv.BUCKET.get(`reports/${STEAM_ID}/20260913_120000_aaaa1111/ACKED`)).not.toBeNull();
    const inbox = await handle(new Request("https://playtest.tar-atari.com/v1/inbox", { headers: ADMIN }), workerEnv, noNetwork);
    expect(((await inbox.json()) as { items: unknown[] }).items).toHaveLength(0);
  });

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
