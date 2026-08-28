import { describe, expect, it } from "vitest";
import type { BuildMenuDisplayEntry } from "./buildMenuGrouping";
import { shortageItemsOf } from "./buildMenuShortage";

const entryWith = (requiredItems: BuildMenuDisplayEntry["requiredItems"]): BuildMenuDisplayEntry => ({
  id: "30000000-0000-4000-8000-000000000001",
  kind: "block",
  categoryGuid: "10000000-0000-4000-8000-000000000001",
  subCategoryGuid: "20000000-0000-4000-8000-000000000001",
  requiredItems,
  displayLabel: "belt",
}) as BuildMenuDisplayEntry;

describe("shortageItemsOf", () => {
  it("lackingの立った素材だけを配信順で返す", () => {
    const items = shortageItemsOf(entryWith([
      { itemId: 3, count: 5, held: 2, lacking: true },
      { itemId: 4, count: 1, held: 9, lacking: false },
      { itemId: 5, count: 3, held: 0, lacking: true },
    ]));
    expect(items.map((item) => item.itemId)).toEqual([3, 5]);
  });

  it("不足が無ければ空配列を返す", () => {
    expect(shortageItemsOf(entryWith([{ itemId: 3, count: 5, held: 9, lacking: false }]))).toEqual([]);
  });

  it("必要素材を持たないエントリは空配列を返す", () => {
    expect(shortageItemsOf(entryWith([]))).toEqual([]);
  });
});
