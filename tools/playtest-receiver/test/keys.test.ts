import { describe, expect, it } from "vitest";
import { bundlePrefix, isKind, isSafeSegment, joinSafePath, KIND_PREFIX, parsePendingIndexKey, pendingIndexKey } from "../src/keys";

describe("keys", () => {
  it("kindごとのプレフィックスは表のとおりで、progressは重ねたsを付けない", () => {
    expect(KIND_PREFIX.report).toBe("reports");
    expect(KIND_PREFIX.progress).toBe("progress");
  });

  it("知らないkindは弾く", () => {
    expect(isKind("report")).toBe(true);
    expect(isKind("progress")).toBe(true);
    expect(isKind("config")).toBe(false);
  });

  it("バンドルのプレフィックスを組み立てる", () => {
    expect(bundlePrefix("report", "76561198000000001", "20260913_120000_aaaa1111"))
      .toBe("reports/76561198000000001/20260913_120000_aaaa1111");
  });

  it("索引キーは往復する", () => {
    const key = pendingIndexKey("progress", "76561198000000001", "20260913_120000_aaaa1111");
    expect(key).toBe("index/pending/progress/76561198000000001/20260913_120000_aaaa1111");
    expect(parsePendingIndexKey(key)).toEqual({ kind: "progress", steamId: "76561198000000001", id: "20260913_120000_aaaa1111" });
  });

  it("形の違う索引キーはnullになる", () => {
    expect(parsePendingIndexKey("index/pending/report/only-two")).toBeNull();
    expect(parsePendingIndexKey("reports/a/b/c")).toBeNull();
  });

  it("危ないセグメントを拒否する", () => {
    expect(isSafeSegment("logs")).toBe(true);
    expect(isSafeSegment("..")).toBe(false);
    expect(isSafeSegment(".")).toBe(false);
    expect(isSafeSegment("")).toBe(false);
    expect(isSafeSegment("a\\b")).toBe(false);
  });

  it("安全なセグメントだけを連結する", () => {
    expect(joinSafePath(["snapshots", "tick_600.json"])).toBe("snapshots/tick_600.json");
    expect(joinSafePath(["..", "etc"])).toBeNull();
    expect(joinSafePath([])).toBeNull();
  });
});
