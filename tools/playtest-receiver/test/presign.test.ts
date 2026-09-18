import { describe, expect, it } from "vitest";
import { UPLOAD_URL_TTL_SECONDS } from "../src/contract";
import { presignPut } from "../src/presign";
import { workerEnv } from "./support/uploadsFixture";

describe("presignPut", () => {
  it("バケットとキーを含むPUT用の署名付きURLをTTLと署名ヘッダ付きで返す", async () => {
    const url = new URL(await presignPut(workerEnv, "reports/7656/20260913_120000_aaaa1111/frames/frame 1.jpg", 1234, new Date("2026-09-18T00:00:00Z")));
    expect(url.host).toBe(`${workerEnv.R2_ACCOUNT_ID}.r2.cloudflarestorage.com`);
    expect(decodeURIComponent(url.pathname)).toBe(`/${workerEnv.R2_BUCKET_NAME}/reports/7656/20260913_120000_aaaa1111/frames/frame 1.jpg`);
    expect(url.searchParams.get("X-Amz-Expires")).toBe(String(UPLOAD_URL_TTL_SECONDS));
    expect(url.searchParams.get("X-Amz-SignedHeaders")).toBe("content-length;host");
    expect(url.searchParams.get("X-Amz-Date")).toBe("20260918T000000Z");
    expect(url.searchParams.get("X-Amz-Signature")).toMatch(/^[0-9a-f]{64}$/);
  });

  it.each(["logs/a#b.log", "logs/a?b=c.log", "ログ/ユニティ 1.log", "logs/100%.log"])(
    "URLの意味を持つ文字や日本語を含むキー %s は別キーへ化けずクエリやフラグメントに漏れない",
    async (relative) => {
      const key = `reports/7656/20260913_120000_aaaa1111/${relative}`;
      const url = new URL(await presignPut(workerEnv, key, 1, new Date("2026-09-18T00:00:00Z")));
      expect(decodeURIComponent(url.pathname)).toBe(`/${workerEnv.R2_BUCKET_NAME}/${key}`);
      expect(url.hash).toBe("");
      expect([...url.searchParams.keys()].every((name) => name.startsWith("X-Amz-"))).toBe(true);
    },
  );

  it("Content-Lengthが違えば署名も違う", async () => {
    const a = await presignPut(workerEnv, "reports/7656/x/a.bin", 1, new Date("2026-09-18T00:00:00Z"));
    const b = await presignPut(workerEnv, "reports/7656/x/a.bin", 2, new Date("2026-09-18T00:00:00Z"));
    expect(new URL(a).searchParams.get("X-Amz-Signature")).not.toBe(new URL(b).searchParams.get("X-Amz-Signature"));
  });
});
