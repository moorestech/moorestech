// クラフト・機械レシピとレシピビューア用アイテム一覧のモックsnapshot
// Mock snapshots for crafting, machine recipes, and the recipe viewer item list
import type { CraftRecipesData, MachineRecipesData, RecipeViewerItemListData } from "../../../src/bridge/contract/payloadTypes";
import { ELECTRIC_MACHINE_BLOCK_GUID, GEAR_MACHINE_BLOCK_GUID } from "./blockLocalizationFixtures";

export const craftRecipes = {
  recipes: [
    {
      recipeGuid: "83000000-0000-4000-8000-000000000001",
      resultItemId: 100,
      resultCount: 1,
      craftTime: 0.2,
      requiredItems: [{ itemId: 1, count: 2 }, { itemId: 2, count: 1 }],
    },
    {
      recipeGuid: "83000000-0000-4000-8000-000000000002",
      resultItemId: 101,
      resultCount: 1,
      craftTime: 0.2,
      requiredItems: [{ itemId: 1, count: 999 }],
    },
    // クラフト2件でアンカーは1件固定
    // Two craft recipes; anchor stays pinned to one
    {
      recipeGuid: "83000000-0000-4000-8000-000000000003",
      resultItemId: 102,
      resultCount: 1,
      craftTime: 0.2,
      requiredItems: [{ itemId: 1, count: 999 }],
    },
    {
      recipeGuid: "83000000-0000-4000-8000-000000000004",
      resultItemId: 102,
      resultCount: 2,
      craftTime: 0.4,
      requiredItems: [{ itemId: 2, count: 999 }],
    },
  ],
} satisfies CraftRecipesData;

export const machineRecipes = {
  recipes: [
    {
      // Plankはクラフト・機械両方で作れる
      // 既存3件カウント維持のため追加
      // Plank has both a craft and a machine recipe.
      // Added on the gear-machine block so the existing electric-machine recipe-count test stays at 3
      recipeGuid: "eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee",
      blockGuid: GEAR_MACHINE_BLOCK_GUID,
      blockId: 4, time: 8,
      inputItems: [{ itemId: 2, count: 1 }], outputItems: [{ itemId: 100, count: 1 }],
    },
    {
      recipeGuid: "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa",
      blockGuid: ELECTRIC_MACHINE_BLOCK_GUID,
      blockId: 3, time: 5,
      inputItems: [{ itemId: 1, count: 2 }], outputItems: [{ itemId: 3, count: 1 }],
    },
    {
      recipeGuid: "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb",
      blockGuid: ELECTRIC_MACHINE_BLOCK_GUID,
      blockId: 3, time: 10,
      inputItems: [{ itemId: 2, count: 3 }], outputItems: [{ itemId: 7, count: 2 }],
    },
    {
      recipeGuid: "cccccccc-cccc-4ccc-8ccc-cccccccccccc",
      blockGuid: ELECTRIC_MACHINE_BLOCK_GUID,
      blockId: 3, time: 15,
      inputItems: [{ itemId: 1, count: 1 }, { itemId: 2, count: 1 }], outputItems: [{ itemId: 11, count: 4 }],
    },
    {
      recipeGuid: "dddddddd-dddd-4ddd-8ddd-dddddddddddd",
      blockGuid: GEAR_MACHINE_BLOCK_GUID,
      blockId: 4, time: 20,
      inputItems: [{ itemId: 3, count: 2 }], outputItems: [{ itemId: 7, count: 1 }],
    },
  ],
} satisfies MachineRecipesData;

export const itemList = { itemIds: [100, 101, 102, 1, 2] } satisfies RecipeViewerItemListData;
// チュートリアル指名の対象。1段目のセルで、ScrollAreaの上端クリップに最も晒される位置
// The tutorial's named target: a first-row cell, the position most exposed to the ScrollArea's top clip
export const TUTORIAL_RECIPE_ITEM_ID = 100;
// 溢れる直前の段数(6列x7段)。スクロール領域へちょうど収まる境界を押さえる用
// Exactly the last non-overflowing row count (6 cols x 7 rows), pinning the boundary that just fits the scroller
export const sevenRowItemList = { itemIds: [100, ...Array.from({ length: 41 }, (_, i) => i + 1)] } satisfies RecipeViewerItemListData;
