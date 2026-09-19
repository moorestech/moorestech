import { afterEach, describe, expect, it, vi } from "vitest";
import { ackedMarkerKey, declaredMarkerKey, READY_MARKER } from "../../src/bundleMarkers";
import { bundlePrefix, pendingIndexKey } from "../../src/keys";
import { bearer, clean, complete, declaration, handle, ID, noNetwork, prepare, putDirect, STEAM_ID, workerEnv } from "../support/uploadsFixture";

// 宣言・パス・kind・メソッドの拒否系は uploads/validation.test.ts に分けている（200行規約）
// Declaration/path/kind/method rejection cases live in uploads/validation.test.ts (the 200-line rule)
afterEach(() => {
  vi.restoreAllMocks();
  return clean();
});

type PrepareBody = { outcome: string; uploads: { path: string; url: string; bytes: number }[]; expiresInSeconds: number };

describe("prepare", () => {
  it("宣言どおりのファイルごとに署名付きURLを返し、DECLAREDを置く", async () => {
    const response = await prepare("report", ID, declaration({ "manifest.json": 10, "frames/frame 1.jpg": 20 }));
    expect(response.status).toBe(200);
    const body = (await response.json()) as PrepareBody;
    expect(body.outcome).toBe("prepared");
    expect(body.expiresInSeconds).toBe(3600);
    expect(body.uploads.map((u) => u.path)).toEqual(["manifest.json", "frames/frame 1.jpg"]);
    expect(body.uploads.map((u) => u.bytes)).toEqual([10, 20]);
    expect(new URL(body.uploads[1]!.url).host).toBe(`${workerEnv.R2_ACCOUNT_ID}.r2.cloudflarestorage.com`);
    expect(await workerEnv.BUCKET.get(declaredMarkerKey("report", STEAM_ID, ID))).not.toBeNull();
  });

  it("URLの置き場はtokenのsteamIdだけが決め、実ファイル名（空白・#・日本語）をエンコードしたキーになる", async () => {
    const other = "76561198000000009";
    const response = await prepare("progress", ID, declaration({ "snapshots/shot#1 a.png": 3, "あ.png": 1 }), other);
    const body = (await response.json()) as PrepareBody;
    const paths = body.uploads.map((u) => decodeURIComponent(new URL(u.url).pathname));
    expect(paths).toEqual([
      `/${workerEnv.R2_BUCKET_NAME}/progress/${other}/${ID}/snapshots/shot#1 a.png`,
      `/${workerEnv.R2_BUCKET_NAME}/progress/${other}/${ID}/あ.png`,
    ]);
    expect(await workerEnv.BUCKET.get(declaredMarkerKey("progress", other, ID))).not.toBeNull();
    expect(await workerEnv.BUCKET.get(declaredMarkerKey("progress", STEAM_ID, ID))).toBeNull();
  });

  it("ACKED済みの箱へのprepareはURLを発行せずackedを返しwarnする", async () => {
    await workerEnv.BUCKET.put(ackedMarkerKey("report", STEAM_ID, ID), "");
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    const response = await prepare("report", ID, declaration({ a: 1 }));
    expect(response.status).toBe(200);
    expect(await response.json()).toEqual({ outcome: "acked" });
    expect(await workerEnv.BUCKET.get(declaredMarkerKey("report", STEAM_ID, ID))).toBeNull();
    expect(warn).toHaveBeenCalledWith(expect.stringContaining("already acked"));
  });

  it("Bearer無しは401でwarnする", async () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    const response = await handle(new Request(`https://x/v1/uploads/report/${ID}/prepare`, { method: "POST", body: declaration({ a: 1 }) }), workerEnv, noNetwork);
    expect(response.status).toBe(401);
    expect(warn).toHaveBeenCalled();
  });
});

describe("complete", () => {
  it("宣言した全ファイルが宣言どおりの長さで揃えばREADYと索引を書き、filesはWorkerが数えた一覧になる", async () => {
    await prepare("report", ID, declaration({ "manifest.json": 2, "a.bin": 3 }));
    await putDirect("report", STEAM_ID, ID, "manifest.json", "{}");
    await putDirect("report", STEAM_ID, ID, "a.bin", "abc");
    const response = await complete("report", ID, JSON.stringify({ manifest: "{}", skipped: [{ path: "x", reason: "too-large" }] }));
    expect(response.status).toBe(200);
    expect(await response.json()).toEqual({ ready: true, fileCount: 2 });
    const ready = await workerEnv.BUCKET.get(`${bundlePrefix("report", STEAM_ID, ID)}/${READY_MARKER}`);
    expect(await ready!.json()).toEqual({ kind: "report", id: ID, fileCount: 2, files: ["a.bin", "manifest.json"], skipped: [{ path: "x", reason: "too-large" }], manifest: "{}" });
    expect(await workerEnv.BUCKET.get(pendingIndexKey("report", STEAM_ID, ID))).not.toBeNull();
  });

  it("欠けたファイルがあれば409 incompleteで欠損を返し、READYを書かずwarnする", async () => {
    await prepare("report", ID, declaration({ "manifest.json": 2, "a.bin": 3 }));
    await putDirect("report", STEAM_ID, ID, "manifest.json", "{}");
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    const response = await complete("report", ID);
    expect(response.status).toBe(409);
    expect(await response.json()).toEqual({ reason: "incomplete", missing: [{ path: "a.bin", expectedBytes: 3, actualBytes: null }] });
    expect(await workerEnv.BUCKET.get(`${bundlePrefix("report", STEAM_ID, ID)}/${READY_MARKER}`)).toBeNull();
    expect(await workerEnv.BUCKET.get(pendingIndexKey("report", STEAM_ID, ID))).toBeNull();
    expect(warn).toHaveBeenCalledWith(expect.stringContaining("incomplete"));
  });

  it("長さが宣言と違うファイルは欠損として返す", async () => {
    await prepare("report", ID, declaration({ "a.bin": 3 }));
    await putDirect("report", STEAM_ID, ID, "a.bin", "abcd");
    const response = await complete("report", ID);
    expect(response.status).toBe(409);
    expect(await response.json()).toEqual({ reason: "incomplete", missing: [{ path: "a.bin", expectedBytes: 3, actualBytes: 4 }] });
  });

  it("prepareしていない箱のcompleteは409 not-preparedでwarnする", async () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    const response = await complete("report", ID);
    expect(response.status).toBe(409);
    expect(await response.json()).toEqual({ reason: "not-prepared" });
    expect(warn).toHaveBeenCalled();
  });

  it("ACKED済みの箱のcompleteは書かずに冪等成功しwarnする", async () => {
    await prepare("report", ID, declaration({ "a.bin": 1 }));
    await putDirect("report", STEAM_ID, ID, "a.bin", "a");
    await workerEnv.BUCKET.put(ackedMarkerKey("report", STEAM_ID, ID), "");
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    const response = await complete("report", ID);
    expect(response.status).toBe(200);
    expect(await response.json()).toEqual({ ready: true });
    expect(await workerEnv.BUCKET.get(pendingIndexKey("report", STEAM_ID, ID))).toBeNull();
    expect(await workerEnv.BUCKET.get(`${bundlePrefix("report", STEAM_ID, ID)}/${READY_MARKER}`)).toBeNull();
    expect(warn).toHaveBeenCalledWith(expect.stringContaining("already acked"));
  });

  it("宣言に無いオブジェクトはREADYのfilesに載らない", async () => {
    await prepare("report", ID, declaration({ "a.bin": 1 }));
    await putDirect("report", STEAM_ID, ID, "a.bin", "a");
    await putDirect("report", STEAM_ID, ID, "stray.bin", "zz");
    await complete("report", ID);
    const ready = await workerEnv.BUCKET.get(`${bundlePrefix("report", STEAM_ID, ID)}/${READY_MARKER}`);
    expect(((await ready!.json()) as { files: string[] }).files).toEqual(["a.bin"]);
  });

  it("補足の本文が壊れていてもREADYは照合済みの一覧で書き、manifest/skippedは空にしてwarnする", async () => {
    await prepare("report", ID, declaration({ "a.bin": 1 }));
    await putDirect("report", STEAM_ID, ID, "a.bin", "a");
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    const response = await complete("report", ID, "null");
    expect(response.status).toBe(200);
    const ready = await workerEnv.BUCKET.get(`${bundlePrefix("report", STEAM_ID, ID)}/${READY_MARKER}`);
    expect(await ready!.json()).toEqual({ kind: "report", id: ID, fileCount: 1, files: ["a.bin"], skipped: [], manifest: null });
    expect(warn).toHaveBeenCalled();
  });
});

describe("legacy PUT", () => {
  it("PUT中継は410 direct-upload-requiredで何も書かずwarnする", async () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    const response = await handle(new Request(`https://x/v1/uploads/report/${ID}/a.bin`, { method: "PUT", headers: { authorization: await bearer(), "content-length": "1" }, body: "a" }), workerEnv, noNetwork);
    expect(response.status).toBe(410);
    expect(await response.json()).toEqual({ reason: "direct-upload-required" });
    expect((await workerEnv.BUCKET.list({ limit: 10 })).objects).toHaveLength(0);
    expect(warn).toHaveBeenCalledWith(expect.stringContaining("direct upload required"));
  });
});
