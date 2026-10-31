import { beforeEach, describe, expect, it, vi } from "vitest";
import { clean, handle, noNetwork, workerEnv } from "../support/uploadsFixture";
import { BOX_PREFIX, BOX_URL, adminRequest, boxKeys, putBoxObject } from "../support/adminFixture";

function purge(body: string | null): Promise<Response> {
  return adminRequest("DELETE", "", {}, body);
}

async function readMarker(): Promise<{ purgedAt: string; reason: string | null }> {
  const object = await workerEnv.BUCKET.get(`${BOX_PREFIX}/PURGED`);
  return (await object!.json()) as { purgedAt: string; reason: string | null };
}

// 片付いた箱の生データ削除。ACKED済みだけを消し、ACKEDとPURGEDは残す
// Purging a settled box's raw data; only ACKED boxes are purged, and ACKED and PURGED survive
describe("admin inbox purge", () => {
  beforeEach(clean);

  it("ACKEDの無い箱は409 not-ackedで何も消さない", async () => {
    await putBoxObject("a.txt", "x");
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    const response = await purge(null);
    expect(response.status).toBe(409);
    expect(await response.json()).toEqual({ reason: "not-acked" });
    expect(await boxKeys()).toEqual(["a.txt"]);
    expect(warn).toHaveBeenCalled();
    warn.mockRestore();
  });

  it("ACKED済みの箱は__attachments配下も含めて消え、ACKEDとPURGEDだけ残る", async () => {
    await putBoxObject("ACKED", "2026-10-01T00:00:00.000Z");
    await putBoxObject("READY", "{}");
    await putBoxObject("video.mp4", "12345");
    await putBoxObject("__attachments/000000/chunk", "abc");
    const log = vi.spyOn(console, "log").mockImplementation(() => {});
    const response = await purge(JSON.stringify({ reason: "対応済み" }));
    expect(response.status).toBe(200);
    expect(await response.json()).toEqual({ purged: true, deletedObjects: 3, deletedBytes: 10 });
    expect(await boxKeys()).toEqual(["ACKED", "PURGED"]);
    const marker = await readMarker();
    expect(marker.reason).toBe("対応済み");
    expect(Number.isNaN(Date.parse(marker.purgedAt))).toBe(false);
    log.mockRestore();
  });

  it("再実行は0件で200になりpurgedAtは最初の値を保つ", async () => {
    await putBoxObject("ACKED", "2026-10-01T00:00:00.000Z");
    await putBoxObject("a.txt", "x");
    const log = vi.spyOn(console, "log").mockImplementation(() => {});
    expect((await purge(null)).status).toBe(200);
    const first = await readMarker();
    expect(first.reason).toBeNull();

    const again = await purge(JSON.stringify({ reason: "second" }));
    expect(again.status).toBe(200);
    expect(await again.json()).toEqual({ purged: true, deletedObjects: 0, deletedBytes: 0 });
    expect(await readMarker()).toEqual(first);
    log.mockRestore();
  });

  it("1000件を超える箱も一括deleteを分けて全部消す", async () => {
    await putBoxObject("ACKED", "2026-10-01T00:00:00.000Z");
    const paths = Array.from({ length: 1001 }, (_, i) => `frames/${String(i).padStart(4, "0")}.jpg`);
    await Promise.all(paths.map((path) => putBoxObject(path, "y")));
    const log = vi.spyOn(console, "log").mockImplementation(() => {});
    // 手元のR2は1回のdeleteに上限が無いので、呼び出しごとのキー数をスパイで確かめる
    // The local R2 has no per-call delete cap, so the key count of each call is checked with a spy
    const deleteSpy = vi.spyOn(workerEnv.BUCKET, "delete");
    const response = await purge(null);
    expect(await response.json()).toEqual({ purged: true, deletedObjects: 1001, deletedBytes: 1001 });
    expect(await boxKeys()).toEqual(["ACKED", "PURGED"]);
    const batchSizes = deleteSpy.mock.calls.map(([keys]) => (Array.isArray(keys) ? keys.length : 1));
    expect(batchSizes).toEqual([1000, 1]);
    deleteSpy.mockRestore();
    log.mockRestore();
  });

  it.each(["not json", JSON.stringify({ reason: 1 }), JSON.stringify({ reason: "x".repeat(201) })])("壊れた本文 %s は400で何も消さない", async (body) => {
    await putBoxObject("ACKED", "2026-10-01T00:00:00.000Z");
    await putBoxObject("a.txt", "x");
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    const response = await purge(body);
    expect(response.status).toBe(400);
    expect(await response.json()).toEqual({ reason: "bad-request" });
    expect(await boxKeys()).toEqual(["ACKED", "a.txt"]);
    expect(warn).toHaveBeenCalled();
    warn.mockRestore();
  });

  it("箱のパスへのDELETE以外のメソッドは405", async () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    expect((await adminRequest("GET", "", {}, null)).status).toBe(405);
    warn.mockRestore();
  });

  it("鍵なしのDELETEは401", async () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    const response = await handle(new Request(BOX_URL, { method: "DELETE" }), workerEnv, noNetwork);
    expect(response.status).toBe(401);
    warn.mockRestore();
  });
});
