import { afterEach, describe, expect, it, vi } from "vitest";
import { MAX_BUNDLE_BYTES, MAX_BUNDLE_FILES, MAX_FILE_BYTES } from "../../src/contract";
import { bearer, clean, declaration, handle, ID, noNetwork, prepare, workerEnv } from "../support/uploadsFixture";

// kind・id・宣言・メソッド・設定欠落の拒否系。拒否は必ずwarnし、R2へ何も書かない。成功系は uploads.test.ts
// Rejections for bad kind/id/declaration/method/misconfiguration; each warns and writes nothing to R2. Success cases live in uploads.test.ts
afterEach(() => {
  vi.restoreAllMocks();
  return clean();
});

async function expectRejected(response: Response, status: number, reason: string): Promise<void> {
  expect(response.status).toBe(status);
  expect(await response.json()).toEqual({ reason });
  expect((await workerEnv.BUCKET.list({ limit: 10 })).objects).toHaveLength(0);
}

function spyWarn() {
  return vi.spyOn(console, "warn").mockImplementation(() => {});
}

function manyFiles(count: number): string {
  const entries: Record<string, number> = {};
  for (let i = 0; i < count; i++) entries[`f${i}.bin`] = 1;
  return declaration(entries);
}

describe("prepare の経路検査", () => {
  it("kindが不正なら400 bad-kind", async () => {
    const warn = spyWarn();
    await expectRejected(await prepare("config", ID, declaration({ a: 1 })), 400, "bad-kind");
    expect(warn).toHaveBeenCalled();
  });

  // URLコンストラクタは生の「..」を先に畳んでしまうため、逸脱の試行はパーセントエンコードで送る
  // The URL constructor collapses a raw ".." first, so traversal attempts are sent percent-encoded
  it("idが..なら400 bad-path", async () => {
    const warn = spyWarn();
    await expectRejected(await prepare("report", "%2E%2E", declaration({ a: 1 })), 400, "bad-path");
    expect(warn).toHaveBeenCalled();
  });

  it("idが不正なpercentエンコードなら400 bad-path", async () => {
    const warn = spyWarn();
    await expectRejected(await prepare("report", "%ZZ", declaration({ a: 1 })), 400, "bad-path");
    expect(warn).toHaveBeenCalled();
  });

  it("prepareにPOST以外のメソッドは405", async () => {
    const warn = spyWarn();
    const response = await handle(new Request(`https://x/v1/uploads/report/${ID}/prepare`, { method: "GET", headers: { authorization: await bearer() } }), workerEnv, noNetwork);
    await expectRejected(response, 405, "method-not-allowed");
    expect(warn).toHaveBeenCalled();
  });

  it("旧ファイルパスにPUT以外のメソッドは405", async () => {
    const warn = spyWarn();
    const response = await handle(new Request(`https://x/v1/uploads/report/${ID}/a.txt`, { method: "GET", headers: { authorization: await bearer() } }), workerEnv, noNetwork);
    await expectRejected(response, 405, "method-not-allowed");
    expect(warn).toHaveBeenCalled();
  });

  it("SESSION_HMAC_SECRETが無ければ401ではなく500 server-misconfigured", async () => {
    const warn = spyWarn();
    const response = await handle(
      new Request(`https://x/v1/uploads/report/${ID}/prepare`, { method: "POST", headers: { authorization: await bearer() }, body: declaration({ a: 1 }) }),
      { ...workerEnv, SESSION_HMAC_SECRET: "" },
      noNetwork,
    );
    await expectRejected(response, 500, "server-misconfigured");
    expect(warn).toHaveBeenCalled();
  });
});

describe("prepare の宣言検査", () => {
  it("本文がJSONでなければ400 bad-request", async () => {
    const warn = spyWarn();
    await expectRejected(await prepare("report", ID, "not json"), 400, "bad-request");
    expect(warn).toHaveBeenCalled();
  });

  it("..を含むパスは400 bad-path", async () => {
    const warn = spyWarn();
    await expectRejected(await prepare("report", ID, declaration({ "../../etc/passwd": 1 })), 400, "bad-path");
    expect(warn).toHaveBeenCalledWith(expect.stringContaining("bad-path"));
  });

  it("空セグメント(//)を含むパスは400 bad-path", async () => {
    const warn = spyWarn();
    await expectRejected(await prepare("report", ID, declaration({ "a//b.txt": 1 })), 400, "bad-path");
    expect(warn).toHaveBeenCalled();
  });

  it("\\を含むパスは400 bad-path", async () => {
    const warn = spyWarn();
    await expectRejected(await prepare("report", ID, declaration({ "a\\b.txt": 1 })), 400, "bad-path");
    expect(warn).toHaveBeenCalled();
  });

  it("予約名（READY・DECLARED）を宣言すると400 reserved-name", async () => {
    const warn = spyWarn();
    await expectRejected(await prepare("report", ID, declaration({ READY: 1 })), 400, "reserved-name");
    await expectRejected(await prepare("report", ID, declaration({ DECLARED: 1 })), 400, "reserved-name");
    expect(warn).toHaveBeenCalledTimes(2);
  });

  it("1ファイルが100MiBを超えると413 too-large", async () => {
    const warn = spyWarn();
    await expectRejected(await prepare("report", ID, declaration({ "video.mp4": MAX_FILE_BYTES + 1 })), 413, "too-large");
    expect(warn).toHaveBeenCalledWith(expect.stringContaining("too-large"));
  });

  it("ファイル数が上限を超えると413 too-many-files", async () => {
    const warn = spyWarn();
    await expectRejected(await prepare("report", ID, manyFiles(MAX_BUNDLE_FILES + 1)), 413, "too-many-files");
    expect(warn).toHaveBeenCalledWith(expect.stringContaining("too-many-files"));
  });

  it("合計バイトが上限を超えると413 bundle-too-large", async () => {
    const warn = spyWarn();
    const body = declaration({ a: MAX_FILE_BYTES, b: MAX_FILE_BYTES, c: MAX_BUNDLE_BYTES - 2 * MAX_FILE_BYTES + 1 });
    await expectRejected(await prepare("report", ID, body), 413, "bundle-too-large");
    expect(warn).toHaveBeenCalledWith(expect.stringContaining("bundle-too-large"));
  });
});
