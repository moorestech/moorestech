import type { ItemMasterData } from "../../../src/bridge/contract/payloadTypes";
import { itemNameKey } from "../../../src/shared/i18n/contentKeys";

export const WOOD_ITEM_GUID = "00000000-0000-4000-8000-000000000001";
export const STONE_ITEM_GUID = "00000000-0000-4000-8000-000000000002";
export const TIMBER_ITEM_GUID = "00000000-0000-4000-8000-000000000003";
export const PLANK_ITEM_GUID = "00000000-0000-4000-8000-000000000100";
export const IMPOSSIBLE_PLANK_ITEM_GUID = "00000000-0000-4000-8000-000000000101";
export const TWIN_RECIPE_PLANK_ITEM_GUID = "00000000-0000-4000-8000-000000000102";

export const itemMaster = {
  items: [
    { itemId: 1, itemGuid: WOOD_ITEM_GUID, maxStack: 100 },
    { itemId: 2, itemGuid: STONE_ITEM_GUID, maxStack: 100 },
    { itemId: 100, itemGuid: PLANK_ITEM_GUID, maxStack: 100 },
    { itemId: 101, itemGuid: IMPOSSIBLE_PLANK_ITEM_GUID, maxStack: 100 },
    { itemId: 102, itemGuid: TWIN_RECIPE_PLANK_ITEM_GUID, maxStack: 100 },
  ],
} satisfies ItemMasterData;

export const demoItemMaster = {
  items: Array.from({ length: 120 }, (_, index) => ({
    itemId: index + 1,
    itemGuid: demoItemGuid(index + 1),
    maxStack: 100,
  })),
} satisfies ItemMasterData;

// 動的コンテンツ名はバニラCSV外なのでmockの合成辞書へ足す
// Dynamic content names live outside the vanilla CSV, so add them to the mock composite dictionary
export const itemNameDictionaries = createItemNameDictionaries();

function createItemNameDictionaries(): Record<string, Record<string, string>> {
  const fixedNames = [
    [WOOD_ITEM_GUID, "Wood"],
    [STONE_ITEM_GUID, "Stone"],
    [TIMBER_ITEM_GUID, "Timber"],
    [PLANK_ITEM_GUID, "Plank"],
    [IMPOSSIBLE_PLANK_ITEM_GUID, "Impossible Plank"],
    [TWIN_RECIPE_PLANK_ITEM_GUID, "Twin Recipe Plank"],
  ] as const;
  const demoNames = Array.from({ length: 120 }, (_, index) => [
    demoItemGuid(index + 1),
    `Item ${index + 1}`,
  ] as const);
  const dictionary = Object.fromEntries(
    [...fixedNames, ...demoNames].map(([guid, name]) => [itemNameKey(guid), name]),
  );
  return { source: dictionary, english: dictionary, japanese: dictionary, german: dictionary };
}

function demoItemGuid(itemId: number): string {
  return `10000000-0000-4000-8000-${itemId.toString().padStart(12, "0")}`;
}
