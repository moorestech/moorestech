import { beforeEach, describe, expect, it, vi } from "vitest";
import { clean, workerEnv } from "../support/uploadsFixture";
import { BOX_PREFIX, adminRequest, boxKeys, putBoxObject } from "../support/adminFixture";

type Missing = { path: string; expectedBytes: number; actualBytes: number | null };

function importFiles(files: Record<string, number>): Promise<Response> {
  return adminRequest("POST", "/import", { "content-type": "application/json" }, JSON.stringify({ files: Object.entries(files).map(([path, bytes]) => ({ path, bytes })) }));
}

function complete(): Promise<Response> {
  return adminRequest("POST", "/import/complete", {}, null);
}

async function acked(): Promise<string | null> {
  const object = await workerEnv.BUCKET.get(`${BOX_PREFIX}/ACKED`);
  return object === null ? null : object.text();
}

// zip取り込みの確定。importで残した宣言の全ファイルが揃って初めてACKEDを書く
// Settling a zip import: ACKED is written only once every file of the stored declaration is present
describe("admin inbox import/complete", () => {
  beforeEach(clean);

  it("宣言の全ファイルが宣言どおりの長さで揃えばACKEDを書き、未ACK索引も消す", async () => {
    await importFiles({ READY: 2, "manifest.json": 3 });
    await putBoxObject("READY", "{}");
    await putBoxObject("manifest.json", "abc");
    await workerEnv.BUCKET.put(`index/pending/report/${BOX_PREFIX.split("/").slice(1).join("/")}`, "");
    const response = await complete();
    expect(response.status).toBe(200);
    expect(await response.json()).toEqual({ acked: true, fileCount: 2 });
    expect(await acked()).not.toBeNull();
    expect((await workerEnv.BUCKET.list({ prefix: "index/pending/" })).objects).toHaveLength(0);
  });

  it("PUTが足りない・長さが食い違うと409で一覧を返し、ACKEDを書かない", async () => {
    await importFiles({ "manifest.json": 3, "video.mp4": 5, "frames/1.jpg": 1 });
    await putBoxObject("manifest.json", "abc");
    await putBoxObject("video.mp4", "abcdefgh");
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    const response = await complete();
    expect(response.status).toBe(409);
    const body = (await response.json()) as { reason: string; missing: Missing[] };
    expect(body.reason).toBe("incomplete");
    expect(body.missing).toEqual([
      { path: "video.mp4", expectedBytes: 5, actualBytes: 8 },
      { path: "frames/1.jpg", expectedBytes: 1, actualBytes: null },
    ]);
    expect(await acked()).toBeNull();
    expect(warn).toHaveBeenCalledWith(expect.stringContaining("video.mp4(8/5)"));
    warn.mockRestore();
  });

  it("importの応答がconflictsでも、completeは食い違いを409で返してACKEDを書かない", async () => {
    await putBoxObject("diff.txt", "abcd");
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    const imported = (await (await importFiles({ "diff.txt": 2 })).json()) as { conflicts: Missing[] };
    expect(imported.conflicts).toEqual([{ path: "diff.txt", expectedBytes: 2, actualBytes: 4 }]);
    const response = await complete();
    expect(response.status).toBe(409);
    expect(((await response.json()) as { missing: Missing[] }).missing).toEqual([{ path: "diff.txt", expectedBytes: 2, actualBytes: 4 }]);
    expect(await acked()).toBeNull();
    warn.mockRestore();
  });

  it("importとcompleteの間に片付けられた箱は409 purgedでACKEDを書かない", async () => {
    await importFiles({ "a.txt": 1 });
    await putBoxObject("a.txt", "x");
    await putBoxObject("PURGED", JSON.stringify({ purgedAt: "2026-10-01T00:00:00.000Z", reason: null }));
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    const response = await complete();
    expect(response.status).toBe(409);
    expect(await response.json()).toEqual({ reason: "purged" });
    expect(await acked()).toBeNull();
    warn.mockRestore();
  });

  it("既にACKEDのある箱は最初の日時を保ったまま200", async () => {
    await putBoxObject("ACKED", "2026-10-01T00:00:00.000Z");
    await importFiles({ "a.txt": 1 });
    await putBoxObject("a.txt", "x");
    expect((await complete()).status).toBe(200);
    expect(await acked()).toBe("2026-10-01T00:00:00.000Z");
  });

  it("importの前のcompleteは409 not-imported、壊れた宣言は409 declaration-unreadable", async () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    const before = await complete();
    expect(before.status).toBe(409);
    expect(await before.json()).toEqual({ reason: "not-imported" });
    await putBoxObject("IMPORT_DECLARED", "not json");
    const broken = await complete();
    expect(broken.status).toBe(409);
    expect(await broken.json()).toEqual({ reason: "declaration-unreadable" });
    expect(await boxKeys()).toEqual(["IMPORT_DECLARED"]);
    warn.mockRestore();
  });

  it("import/completeへのPOST以外は405", async () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    expect((await adminRequest("GET", "/import/complete", {}, null)).status).toBe(405);
    warn.mockRestore();
  });
});
