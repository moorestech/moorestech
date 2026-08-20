import { describe, expect, it } from "vitest";
import { firstSlotIndexByItemId } from "./inventoryItemAnchors";

describe("firstSlotIndexByItemId", () => {
  // 同じアイテムが複数スロットにあっても先頭だけを採り、空スロット(0)は無視する
  // Only the first slot per item is taken even when it appears in several; empty slots (0) are ignored
  it("maps each item to its first slot and skips empty slots", () => {
    const slots = [{ itemId: 0 }, { itemId: 7 }, { itemId: 3 }, { itemId: 7 }];
    expect([...firstSlotIndexByItemId(slots)]).toEqual([[7, 1], [3, 2]]);
  });
});
