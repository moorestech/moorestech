import { describe, expect, it } from "vitest";
import { firstSlotIndexByItemId } from "./inventoryItemAnchors";

describe("firstSlotIndexByItemId", () => {
  // 先頭スロットだけを採り空スロットは無視する
  // Takes only the first slot and ignores empty ones
  it("maps each item to its first slot and skips empty slots", () => {
    const slots = [{ itemId: 0 }, { itemId: 7 }, { itemId: 3 }, { itemId: 7 }];
    expect([...firstSlotIndexByItemId(slots)]).toEqual([[7, 1], [3, 2]]);
  });
});
