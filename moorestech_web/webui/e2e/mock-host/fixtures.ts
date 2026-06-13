import type {
  PlayerInventoryData,
  CraftRecipesData,
  MachineRecipesData,
  RecipeViewerItemListData,
  ItemMasterData,
} from "../../src/bridge/payloadTypes";

const empty = () => ({ itemId: 0, count: 0 });

// 9列×4行のメイン + 9列ホットバー。先頭にクラフト素材を仕込む
// 9x4 main + 9 hotbar; seed craft materials at the front
export const inventory = {
  mainSlots: [
    { itemId: 1, count: 10 },
    { itemId: 2, count: 10 },
    ...Array.from({ length: 34 }, empty),
  ],
  hotbarSlots: Array.from({ length: 9 }, empty),
  grab: empty(),
} satisfies PlayerInventoryData;

// collect 実行後の snapshot（同種を1スロットへ集約した想定の canned 結果）
// Post-collect snapshot (canned: same items consolidated into one slot)
export const inventoryAfterCollect = {
  mainSlots: [
    { itemId: 1, count: 20 },
    ...Array.from({ length: 35 }, empty),
  ],
  hotbarSlots: Array.from({ length: 9 }, empty),
  grab: empty(),
} satisfies PlayerInventoryData;

export const craftRecipes = {
  recipes: [
    {
      recipeGuid: "g-craft-1",
      resultItemId: 100,
      resultCount: 1,
      craftTime: 1,
      requiredItems: [
        { itemId: 1, count: 2 },
        { itemId: 2, count: 1 },
      ],
    },
  ],
} satisfies CraftRecipesData;

export const machineRecipes = { recipes: [] } satisfies MachineRecipesData;

export const itemList = { itemIds: [100, 1, 2] } satisfies RecipeViewerItemListData;

export const itemMaster = {
  items: [
    { itemId: 1, name: "Wood", maxStack: 100 },
    { itemId: 2, name: "Stone", maxStack: 100 },
    { itemId: 100, name: "Plank", maxStack: 100 },
  ],
} satisfies ItemMasterData;
