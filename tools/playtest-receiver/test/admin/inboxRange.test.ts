import { beforeEach, describe, expect, it, vi } from "vitest";
import { clean } from "../support/uploadsFixture";
import { adminRequest, decode, putBoxObject } from "../support/adminFixture";

const BODY = "0123456789";

function getVideo(range: string | null): Promise<Response> {
  return adminRequest("GET", "/video.mp4", range === null ? {} : { range }, null);
}

// 動画のシーク中継のため、個別ファイル取得は単一範囲のRangeに206で応じる
// The per-file fetch answers a single Range with 206 so the dashboard can relay a seeking video
describe("admin inbox object range", () => {
  beforeEach(async () => {
    await clean();
    await putBoxObject("video.mp4", BODY);
  });

  it("Rangeなしは200で全体とContent-LengthとAccept-Rangesを返す", async () => {
    const response = await getVideo(null);
    expect(response.status).toBe(200);
    expect(response.headers.get("content-length")).toBe("10");
    expect(response.headers.get("accept-ranges")).toBe("bytes");
    expect(decode(await response.arrayBuffer())).toBe(BODY);
  });

  it.each([
    ["bytes=2-5", "2345", "bytes 2-5/10"],
    ["bytes=7-", "789", "bytes 7-9/10"],
    ["bytes=-3", "789", "bytes 7-9/10"],
    ["bytes=8-100", "89", "bytes 8-9/10"],
    ["bytes=-100", BODY, "bytes 0-9/10"],
  ])("%s は206でその範囲を返す", async (range, expected, contentRange) => {
    const response = await getVideo(range);
    expect(response.status).toBe(206);
    expect(response.headers.get("content-range")).toBe(contentRange);
    expect(response.headers.get("content-length")).toBe(String(expected.length));
    expect(decode(await response.arrayBuffer())).toBe(expected);
  });

  it.each(["bytes=10-", "bytes=5-2", "bytes=-0", "bytes=-", "bytes=0-1,3-4", "items=0-1", "bytes=a-b"])(
    "%s は416でContent-Rangeに全長を返しwarnする",
    async (range) => {
      const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
      const response = await getVideo(range);
      expect(response.status).toBe(416);
      expect(response.headers.get("content-range")).toBe("bytes */10");
      expect(warn).toHaveBeenCalled();
      warn.mockRestore();
    },
  );

  it("PURGEDのある箱の不在ファイルは410でpurgedAtを返す", async () => {
    await putBoxObject("PURGED", JSON.stringify({ purgedAt: "2026-10-01T00:00:00.000Z", reason: "done" }));
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    const response = await adminRequest("GET", "/world.zip", {}, null);
    expect(response.status).toBe(410);
    expect(await response.json()).toEqual({ reason: "purged", purgedAt: "2026-10-01T00:00:00.000Z" });
    warn.mockRestore();
  });

  it("PURGEDが読めなくても410でpurgedAtはnull", async () => {
    await putBoxObject("PURGED", "not json");
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    const response = await adminRequest("GET", "/world.zip", {}, null);
    expect(response.status).toBe(410);
    expect(await response.json()).toEqual({ reason: "purged", purgedAt: null });
    expect(warn).toHaveBeenCalled();
    warn.mockRestore();
  });

  it("PURGEDの無い箱の不在ファイルは404のまま", async () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    const response = await adminRequest("GET", "/world.zip", {}, null);
    expect(response.status).toBe(404);
    expect(await response.json()).toEqual({ reason: "not-found" });
    warn.mockRestore();
  });
});
