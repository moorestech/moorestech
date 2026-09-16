import { beforeEach, describe, expect, it, vi } from "vitest";
import { handle } from "../../src/index";
import { pendingIndexKey } from "../../src/keys";
import { bearer, clean, ID, noNetwork, STEAM_ID, workerEnv } from "../support/uploadsFixture";

// kind/path/Content-Length/メソッドの拒否系は uploads/validation.test.ts に分けている（200行規約）
// Kind/path/Content-Length/method rejection cases live in uploads/validation.test.ts (the 200-line rule)
describe("uploads", () => {
  beforeEach(clean);

  it("PUTしたファイルがtokenのsteamId配下へ入る", async () => {
    const response = await handle(
      new Request(`https://playtest.tar-atari.com/v1/uploads/report/${ID}/logs/unity.log`, {
        method: "PUT",
        headers: { authorization: await bearer(), "content-length": "5" },
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
        headers: { authorization: await bearer("76561198000000009"), "content-length": "2" },
        body: "{}",
      }),
      workerEnv,
      noNetwork,
    );
    expect(await workerEnv.BUCKET.get(`progress/76561198000000009/${ID}/record.json`)).not.toBeNull();
    expect(await workerEnv.BUCKET.get(`progress/${STEAM_ID}/${ID}/record.json`)).toBeNull();
  });

  // クライアントはセグメントをpercent-encodeして送る。復号した実ファイル名がそのままキーになる
  // The client percent-encodes each segment, and the decoded real file name is what becomes the key
  it("エスケープされた名前は復号した実ファイル名でキーになる", async () => {
    const response = await handle(
      new Request(`https://playtest.tar-atari.com/v1/uploads/report/${ID}/snapshots/shot%231%20a%3Fb.png`, {
        method: "PUT",
        headers: { authorization: await bearer(), "content-length": "3" },
        body: "png",
      }),
      workerEnv,
      noNetwork,
    );
    expect(response.status).toBe(200);
    expect(await (await workerEnv.BUCKET.get(`reports/${STEAM_ID}/${ID}/snapshots/shot#1 a?b.png`))?.text()).toBe("png");
  });

  it("日本語のファイル名も保存できる", async () => {
    const response = await handle(
      new Request(`https://playtest.tar-atari.com/v1/uploads/report/${ID}/%E3%81%82.png`, {
        method: "PUT",
        headers: { authorization: await bearer(), "content-length": "3" },
        body: "png",
      }),
      workerEnv,
      noNetwork,
    );
    expect(response.status).toBe(200);
    expect(await workerEnv.BUCKET.get(`reports/${STEAM_ID}/${ID}/あ.png`)).not.toBeNull();
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
