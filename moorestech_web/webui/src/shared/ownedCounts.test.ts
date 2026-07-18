import { describe, it, expect } from "vitest";
import { buildOwnedCounts, hasEnoughItems } from "./ownedCounts";

const slot = (itemId: number, count: number) => ({ itemId, count });

describe("buildOwnedCounts", () => {
  it("同一 itemId を跨スロットで合算する", () => {
    const owned = buildOwnedCounts([slot(1, 10), slot(2, 3), slot(1, 5)]);
    expect(owned.get(1)).toBe(15);
    expect(owned.get(2)).toBe(3);
  });
  it("空スロット(itemId=0)は無視する", () => {
    expect(buildOwnedCounts([slot(0, 0)]).size).toBe(0);
  });
  it("count<=0 のスロットはエントリを作らない（旧recipe実装と同挙動）", () => {
    expect(buildOwnedCounts([slot(5, 0)]).has(5)).toBe(false);
  });
});

describe("hasEnoughItems", () => {
  it("全素材を満たせば true", () => {
    expect(hasEnoughItems([{ itemId: 1, count: 3 }], new Map([[1, 3]]))).toBe(true);
  });

  it("一つでも不足なら false", () => {
    expect(hasEnoughItems([{ itemId: 1, count: 3 }], new Map([[1, 2]]))).toBe(false);
  });
});
