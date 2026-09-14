import { env } from "cloudflare:test";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { handle } from "../src/index";
import { signToken } from "../src/token";
import { pendingIndexKey } from "../src/keys";
import type { Env } from "../src/env";

const workerEnv = env as unknown as Env;
const STEAM_ID = "76561198000000001";
const ID = "20260913_120000_aaaa1111";
const noNetwork: typeof fetch = (async () => {
  throw new Error("tests must not reach the network");
}) as unknown as typeof fetch;

async function bearer(steamId = STEAM_ID): Promise<string> {
  return `Bearer ${await signToken(workerEnv.SESSION_HMAC_SECRET, steamId, Math.floor(Date.now() / 1000))}`;
}

async function clean(): Promise<void> {
  const listed = await workerEnv.BUCKET.list({ limit: 1000 });
  await Promise.all(listed.objects.map((object) => workerEnv.BUCKET.delete(object.key)));
}

describe("uploads", () => {
  beforeEach(clean);

  it("PUTしたファイルがtokenのsteamId配下へ入る", async () => {
    const response = await handle(
      new Request(`https://playtest.tar-atari.com/v1/uploads/report/${ID}/logs/unity.log`, {
        method: "PUT",
        headers: { authorization: await bearer() },
        body: "hello",
      }),
      workerEnv,
      noNetwork,
    );
    expect(response.status).toBe(200);
    const stored = await workerEnv.BUCKET.get(`reports/${STEAM_ID}/${ID}/logs/unity.log`);
    expect(await stored?.text()).toBe("hello");
  });

  it("URLに他人のsteamIdは現れずtokenだけが置き場を決める", async () => {
    await handle(
      new Request(`https://playtest.tar-atari.com/v1/uploads/progress/${ID}/record.json`, {
        method: "PUT",
        headers: { authorization: await bearer("76561198000000009") },
        body: "{}",
      }),
      workerEnv,
      noNetwork,
    );
    expect(await workerEnv.BUCKET.get(`progress/76561198000000009/${ID}/record.json`)).not.toBeNull();
    expect(await workerEnv.BUCKET.get(`progress/${STEAM_ID}/${ID}/record.json`)).toBeNull();
  });

  it("トークンが無ければ401", async () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    const response = await handle(
      new Request(`https://playtest.tar-atari.com/v1/uploads/report/${ID}/a.txt`, { method: "PUT", body: "x" }),
      workerEnv,
      noNetwork,
    );
    expect(response.status).toBe(401);
    expect(warn).toHaveBeenCalled();
    warn.mockRestore();
  });

  it("kindが不正なら400", async () => {
    const response = await handle(
      new Request(`https://playtest.tar-atari.com/v1/uploads/config/${ID}/a.txt`, {
        method: "PUT",
        headers: { authorization: await bearer() },
        body: "x",
      }),
      workerEnv,
      noNetwork,
    );
    expect(response.status).toBe(400);
  });

  // URLコンストラクタは生の「..」を先に畳んでしまうため、逸脱の試行はパーセントエンコードで送る
  // The URL constructor collapses a raw ".." first, so traversal attempts are sent percent-encoded
  it("..を含むパスは400で何も書かない", async () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    const response = await handle(
      new Request(`https://playtest.tar-atari.com/v1/uploads/report/${ID}/%2E%2E/%2E%2E/etc/passwd`, {
        method: "PUT",
        headers: { authorization: await bearer() },
        body: "x",
      }),
      workerEnv,
      noNetwork,
    );
    expect(response.status).toBe(400);
    expect((await workerEnv.BUCKET.list({ limit: 10 })).objects).toHaveLength(0);
    expect(warn).toHaveBeenCalled();
    warn.mockRestore();
  });

  it("Content-Lengthが100MiBを超えたら413", async () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    const response = await handle(
      new Request(`https://playtest.tar-atari.com/v1/uploads/report/${ID}/video.mp4`, {
        method: "PUT",
        headers: { authorization: await bearer(), "content-length": String(100 * 1024 * 1024 + 1) },
        body: "x",
      }),
      workerEnv,
      noNetwork,
    );
    expect(response.status).toBe(413);
    expect(await response.json()).toEqual({ reason: "too-large" });
    expect(warn).toHaveBeenCalled();
    warn.mockRestore();
  });

  it("idが..なら400で何も書かない", async () => {
    const response = await handle(
      new Request("https://playtest.tar-atari.com/v1/uploads/report/%2E%2E/a.txt", {
        method: "PUT",
        headers: { authorization: await bearer() },
        body: "x",
      }),
      workerEnv,
      noNetwork,
    );
    expect(response.status).toBe(400);
    expect((await workerEnv.BUCKET.list({ limit: 10 })).objects).toHaveLength(0);
  });

  it("completeでREADYと未ACK索引が出来る", async () => {
    const summary = JSON.stringify({ kind: "bug", fileCount: 1 });
    const response = await handle(
      new Request(`https://playtest.tar-atari.com/v1/uploads/report/${ID}/complete`, {
        method: "POST",
        headers: { authorization: await bearer() },
        body: summary,
      }),
      workerEnv,
      noNetwork,
    );
    expect(response.status).toBe(200);
    const ready = await workerEnv.BUCKET.get(`reports/${STEAM_ID}/${ID}/READY`);
    expect(await ready?.text()).toBe(summary);
    expect(await workerEnv.BUCKET.get(pendingIndexKey("report", STEAM_ID, ID))).not.toBeNull();
  });
});
