import { beforeEach, describe, expect, it, vi } from "vitest";
import { handle } from "../../src/index";
import { MAX_BUNDLE_BYTES, MAX_BUNDLE_FILES, MAX_FILE_BYTES, UPLOAD_URL_TTL_SECONDS } from "../../src/contract";
import { clean, noNetwork, workerEnv } from "../support/uploadsFixture";
import { BOX_PREFIX, BOX_URL, ADMIN_HEADERS, adminRequest, boxKeys, putBoxObject } from "../support/adminFixture";

type ImportAnswer = { uploads: { path: string; bytes: number; url: string }[]; conflicts: unknown[]; expiresInSeconds: number };

function importFiles(files: Record<string, number>): Promise<Response> {
  return adminRequest("POST", "/import", { "content-type": "application/json" }, JSON.stringify({ files: Object.entries(files).map(([path, bytes]) => ({ path, bytes })) }));
}

// zip取り込み用の署名付きPUT URL発行。READYは許し、在る分は除き、宣言を箱に残すがACKEDは書かない（確定はimport/complete）
// Presigned PUT URLs for a zip import; READY is allowed, present files are skipped, the declaration is stored but ACKED is not (import/complete settles)
describe("admin inbox import", () => {
  beforeEach(clean);

  it("未送信のファイルにURLを出し、READYも許し、宣言だけを残してACKEDもpending索引も書かない", async () => {
    const response = await importFiles({ READY: 2, "manifest.json": 3, "__attachments/000000/chunk": 4 });
    expect(response.status).toBe(200);
    const body = (await response.json()) as ImportAnswer;
    expect(body.uploads.map((upload) => upload.path).sort()).toEqual(["READY", "__attachments/000000/chunk", "manifest.json"]);
    expect(body.conflicts).toEqual([]);
    expect(body.expiresInSeconds).toBe(UPLOAD_URL_TTL_SECONDS);
    const url = new URL(body.uploads.find((upload) => upload.path === "manifest.json")!.url);
    expect(decodeURIComponent(url.pathname)).toBe(`/${workerEnv.R2_BUCKET_NAME}/${BOX_PREFIX}/manifest.json`);
    expect(url.searchParams.get("X-Amz-SignedHeaders")).toBe("content-length;host;if-none-match");
    expect(await boxKeys()).toEqual(["IMPORT_DECLARED"]);
    const stored = JSON.parse(await (await workerEnv.BUCKET.get(`${BOX_PREFIX}/IMPORT_DECLARED`))!.text());
    expect(stored.files).toEqual([{ path: "READY", bytes: 2 }, { path: "manifest.json", bytes: 3 }, { path: "__attachments/000000/chunk", bytes: 4 }]);
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

  it.each(["ACKED", "DECLARED", "PURGED", "IMPORT_DECLARED", "ack", "import", "complete"])("予約名 %s は400 reserved-name", async (name) => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    const response = await importFiles({ [name]: 1 });
    expect(response.status).toBe(400);
    expect(await response.json()).toMatchObject({ reason: "reserved-name" });
    expect(await boxKeys()).toEqual([]);
    warn.mockRestore();
  });

  it.each([
    ["bad-path", { "../x": 1 }, 400],
    ["empty-declaration", {}, 400],
  ] as const)("prepareと同じ規則で %s を拒否する", async (reason, files, status) => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    const response = await importFiles(files);
    expect(response.status).toBe(status);
    expect(await response.json()).toMatchObject({ reason });
    warn.mockRestore();
  });

  it("送信用の契約上限（1ファイル・件数・合計）を超える箱もzipの全ファイルにURLを出す", async () => {
    const files: Record<string, number> = { "repo/commits.bundle": MAX_FILE_BYTES + 1, "video.mp4": MAX_BUNDLE_BYTES };
    for (let index = 0; index <= MAX_BUNDLE_FILES; index++) files[`frames/${index}.jpg`] = 1;
    const response = await importFiles(files);
    expect(response.status).toBe(200);
    expect(((await response.json()) as ImportAnswer).uploads).toHaveLength(Object.keys(files).length);
  });

  it("R2単一PUTの上限を超えるファイルは413で、どのファイルがどの上限かを応答とログに出す", async () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    const response = await importFiles({ "a.txt": 1, "huge.mp4": 5 * 1000 * 1000 * 1000 + 1 });
    expect(response.status).toBe(413);
    const body = (await response.json()) as { reason: string; detail: string };
    expect(body.reason).toBe("too-large");
    expect(body.detail).toContain("huge.mp4");
    expect(body.detail).toContain("maxFileBytes 5000000000");
    expect(warn).toHaveBeenCalledWith(expect.stringContaining("huge.mp4"));
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
