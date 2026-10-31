import { beforeEach, describe, expect, it, vi } from "vitest";
import { handle } from "../../src/index";
import { MAX_FILE_BYTES, UPLOAD_URL_TTL_SECONDS } from "../../src/contract";
import { clean, noNetwork, workerEnv } from "../support/uploadsFixture";
import { BOX_PREFIX, BOX_URL, ADMIN_HEADERS, adminRequest, boxKeys, putBoxObject } from "../support/adminFixture";

type ImportAnswer = { uploads: { path: string; bytes: number; url: string }[]; conflicts: unknown[]; expiresInSeconds: number };

function importFiles(files: Record<string, number>): Promise<Response> {
  return adminRequest("POST", "/import", { "content-type": "application/json" }, JSON.stringify({ files: Object.entries(files).map(([path, bytes]) => ({ path, bytes })) }));
}

// zip取り込み用の署名付きPUT URL発行。READYは許し、在る分は除き、ACKEDを書いて取り込み一覧に出さない
// Presigned PUT URLs for a zip import; READY is allowed, present files are skipped, and ACKED keeps the box out of the inbox
describe("admin inbox import", () => {
  beforeEach(clean);

  it("未送信のファイルにURLを出し、READYも許し、ACKEDを書くがpending索引は書かない", async () => {
    const response = await importFiles({ READY: 2, "manifest.json": 3, "__attachments/000000/chunk": 4 });
    expect(response.status).toBe(200);
    const body = (await response.json()) as ImportAnswer;
    expect(body.uploads.map((upload) => upload.path).sort()).toEqual(["READY", "__attachments/000000/chunk", "manifest.json"]);
    expect(body.conflicts).toEqual([]);
    expect(body.expiresInSeconds).toBe(UPLOAD_URL_TTL_SECONDS);
    const url = new URL(body.uploads.find((upload) => upload.path === "manifest.json")!.url);
    expect(decodeURIComponent(url.pathname)).toBe(`/${workerEnv.R2_BUCKET_NAME}/${BOX_PREFIX}/manifest.json`);
    expect(url.searchParams.get("X-Amz-SignedHeaders")).toBe("content-length;host;if-none-match");
    expect(await boxKeys()).toEqual(["ACKED"]);
    expect((await workerEnv.BUCKET.list({ prefix: "index/pending/" })).objects).toHaveLength(0);
  });

  it("同じ長さで在るキーは外し、長さ違いはconflictsで返し、既存のACKEDは書き換えない", async () => {
    await putBoxObject("ACKED", "2026-10-01T00:00:00.000Z");
    await putBoxObject("same.txt", "abc");
    await putBoxObject("diff.txt", "abcd");
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    const response = await importFiles({ "same.txt": 3, "diff.txt": 2, "new.txt": 1 });
    const body = (await response.json()) as ImportAnswer;
    expect(body.uploads.map((upload) => upload.path)).toEqual(["new.txt"]);
    expect(body.conflicts).toEqual([{ path: "diff.txt", expectedBytes: 2, actualBytes: 4 }]);
    expect(await (await workerEnv.BUCKET.get(`${BOX_PREFIX}/ACKED`))!.text()).toBe("2026-10-01T00:00:00.000Z");
    warn.mockRestore();
  });

  it.each(["ACKED", "DECLARED", "PURGED"])("予約名 %s は400 reserved-name", async (name) => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    const response = await importFiles({ [name]: 1 });
    expect(response.status).toBe(400);
    expect(await response.json()).toEqual({ reason: "reserved-name" });
    expect(await boxKeys()).toEqual([]);
    warn.mockRestore();
  });

  it.each([
    ["bad-path", { "../x": 1 }, 400],
    ["empty-declaration", {}, 400],
    ["too-large", { "video.mp4": MAX_FILE_BYTES + 1 }, 413],
  ] as const)("prepareと同じ規則で %s を拒否する", async (reason, files, status) => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    const response = await importFiles(files);
    expect(response.status).toBe(status);
    expect(await response.json()).toEqual({ reason });
    warn.mockRestore();
  });

  it("本文がJSONでなければ400 bad-request", async () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    const response = await adminRequest("POST", "/import", {}, "not json");
    expect(response.status).toBe(400);
    expect(await response.json()).toEqual({ reason: "bad-request" });
    warn.mockRestore();
  });

  it("PURGEDのある箱は409 purgedでURLを出さない", async () => {
    await putBoxObject("PURGED", JSON.stringify({ purgedAt: "2026-10-01T00:00:00.000Z", reason: null }));
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    const response = await importFiles({ "a.txt": 1 });
    expect(response.status).toBe(409);
    expect(await response.json()).toEqual({ reason: "purged" });
    expect(await boxKeys()).toEqual(["PURGED"]);
    warn.mockRestore();
  });

  it("R2の署名設定が空なら500 server-misconfigured", async () => {
    const error = vi.spyOn(console, "error").mockImplementation(() => {});
    const request = new Request(`${BOX_URL}/import`, { method: "POST", headers: ADMIN_HEADERS, body: JSON.stringify({ files: [{ path: "a.txt", bytes: 1 }] }) });
    const response = await handle(request, { ...workerEnv, R2_SECRET_ACCESS_KEY: "" }, noNetwork);
    expect(response.status).toBe(500);
    expect(await response.json()).toEqual({ reason: "server-misconfigured" });
    expect(error).toHaveBeenCalledWith(expect.stringContaining("R2_SECRET_ACCESS_KEY"));
    error.mockRestore();
  });

  it("importへのPOST以外は405", async () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    expect((await adminRequest("GET", "/import", {}, null)).status).toBe(405);
    warn.mockRestore();
  });
});
