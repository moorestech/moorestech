import { beforeEach, describe, expect, it, vi } from "vitest";
import { handle } from "../../src/index";
import { bearer, clean, ID, noNetwork, workerEnv } from "../support/uploadsFixture";

// kind不正・path不正・Content-Length不正・メソッド不正の拒否系。成功系は uploads.test.ts
// Rejection cases for bad kind/path/Content-Length/method; success cases live in uploads.test.ts
describe("uploads validation", () => {
  beforeEach(clean);

  it("kindが不正なら400", async () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
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
    expect(await response.json()).toEqual({ reason: "bad-kind" });
    expect(warn).toHaveBeenCalled();
    warn.mockRestore();
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

  it("不正なpercentエンコードのパスは400で何も書かない", async () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    const response = await handle(
      new Request(`https://playtest.tar-atari.com/v1/uploads/report/${ID}/%ZZ.png`, {
        method: "PUT",
        headers: { authorization: await bearer(), "content-length": "1" },
        body: "x",
      }),
      workerEnv,
      noNetwork,
    );
    expect(response.status).toBe(400);
    expect(await response.json()).toEqual({ reason: "bad-path" });
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

  it("Content-Lengthが無ければ411で何も書かない", async () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    const response = await handle(
      new Request(`https://playtest.tar-atari.com/v1/uploads/report/${ID}/no-length.txt`, {
        method: "PUT",
        headers: { authorization: await bearer() },
        body: "x",
      }),
      workerEnv,
      noNetwork,
    );
    expect(response.status).toBe(411);
    expect(await response.json()).toEqual({ reason: "length-required" });
    expect((await workerEnv.BUCKET.list({ limit: 10 })).objects).toHaveLength(0);
    expect(warn).toHaveBeenCalled();
    warn.mockRestore();
  });

  it("Content-Lengthが数値でなければ400で何も書かない", async () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    const response = await handle(
      new Request(`https://playtest.tar-atari.com/v1/uploads/report/${ID}/bad-length.txt`, {
        method: "PUT",
        headers: { authorization: await bearer(), "content-length": "abc" },
        body: "x",
      }),
      workerEnv,
      noNetwork,
    );
    expect(response.status).toBe(400);
    expect(await response.json()).toEqual({ reason: "bad-request" });
    expect((await workerEnv.BUCKET.list({ limit: 10 })).objects).toHaveLength(0);
    expect(warn).toHaveBeenCalled();
    warn.mockRestore();
  });

  it("idが..なら400で何も書かない", async () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
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
    expect(await response.json()).toEqual({ reason: "bad-path" });
    expect((await workerEnv.BUCKET.list({ limit: 10 })).objects).toHaveLength(0);
    expect(warn).toHaveBeenCalled();
    warn.mockRestore();
  });

  it("パスに空セグメント(//)を含むと400で何も書かない", async () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    const response = await handle(
      new Request(`https://playtest.tar-atari.com/v1/uploads/report/${ID}//a.txt`, {
        method: "PUT",
        headers: { authorization: await bearer(), "content-length": "1" },
        body: "x",
      }),
      workerEnv,
      noNetwork,
    );
    expect(response.status).toBe(400);
    expect(await response.json()).toEqual({ reason: "bad-path" });
    expect((await workerEnv.BUCKET.list({ limit: 10 })).objects).toHaveLength(0);
    expect(warn).toHaveBeenCalled();
    warn.mockRestore();
  });

  it("パーセントエンコードされた\\を含むセグメントは400で何も書かない", async () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    const response = await handle(
      new Request(`https://playtest.tar-atari.com/v1/uploads/report/${ID}/a%5Cb.txt`, {
        method: "PUT",
        headers: { authorization: await bearer(), "content-length": "1" },
        body: "x",
      }),
      workerEnv,
      noNetwork,
    );
    expect(response.status).toBe(400);
    expect(await response.json()).toEqual({ reason: "bad-path" });
    expect((await workerEnv.BUCKET.list({ limit: 10 })).objects).toHaveLength(0);
    expect(warn).toHaveBeenCalled();
    warn.mockRestore();
  });

  it("アップロードにPUT以外のメソッドは405", async () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    const response = await handle(
      new Request(`https://playtest.tar-atari.com/v1/uploads/report/${ID}/a.txt`, {
        method: "GET",
        headers: { authorization: await bearer() },
      }),
      workerEnv,
      noNetwork,
    );
    expect(response.status).toBe(405);
    expect(await response.json()).toEqual({ reason: "method-not-allowed" });
    expect(warn).toHaveBeenCalled();
    warn.mockRestore();
  });
});
