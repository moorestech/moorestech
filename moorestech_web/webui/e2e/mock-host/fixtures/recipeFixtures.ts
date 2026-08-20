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

// DEMO専用: Item 1 にリスト最大高を必ず超える件数のレシピを持たせ、スクロールする状態を目視確認できるようにする。
// e2e用のcraftRecipes/machineRecipesへ足すと件数・バッジ系のspecが動くため、DEMO側だけで上乗せする。
// DEMO only: give Item 1 more recipes than the list's max height so the scrolling state is inspectable by eye.
// Adding them to the e2e craftRecipes/machineRecipes would shift the count and badge specs, so DEMO stacks them separately.
const scrollDemoCraftRecipes = [
  { recipeGuid: "83000000-0000-4000-8000-0000000000d1", resultItemId: 1, resultCount: 1, craftTime: 0.5, requiredItems: [{ itemId: 2, count: 1 }] },
  { recipeGuid: "83000000-0000-4000-8000-0000000000d2", resultItemId: 1, resultCount: 2, craftTime: 1.5, requiredItems: [{ itemId: 3, count: 2 }, { itemId: 7, count: 1 }] },
  { recipeGuid: "83000000-0000-4000-8000-0000000000d3", resultItemId: 1, resultCount: 4, craftTime: 3, requiredItems: [{ itemId: 11, count: 3 }] },
  // 素材3点・4点はスロットが段階的に縮む側。縮小後も中央列へ食い込まないことを目視できるようにする
  // Three and four materials fall on the shrinking side; these make the post-shrink clearance inspectable by eye
  { recipeGuid: "83000000-0000-4000-8000-0000000000d4", resultItemId: 1, resultCount: 5, craftTime: 6, requiredItems: [{ itemId: 2, count: 10 }, { itemId: 3, count: 20 }, { itemId: 7, count: 30 }] },
  { recipeGuid: "83000000-0000-4000-8000-0000000000d5", resultItemId: 1, resultCount: 8, craftTime: 9, requiredItems: [{ itemId: 2, count: 1 }, { itemId: 3, count: 2 }, { itemId: 7, count: 3 }, { itemId: 11, count: 4 }] },
];

const scrollDemoMachineRecipes = [
  { recipeGuid: "d1111111-1111-4111-8111-111111111111", blockGuid: ELECTRIC_MACHINE_BLOCK_GUID, blockId: 3, time: 12, inputItems: [{ itemId: 2, count: 4 }], outputItems: [{ itemId: 1, count: 3 }] },
  { recipeGuid: "d2222222-2222-4222-8222-222222222222", blockGuid: GEAR_MACHINE_BLOCK_GUID, blockId: 4, time: 25, inputItems: [{ itemId: 3, count: 1 }, { itemId: 11, count: 2 }], outputItems: [{ itemId: 1, count: 6 }] },
  { recipeGuid: "d3333333-3333-4333-8333-333333333333", blockGuid: ELECTRIC_MACHINE_BLOCK_GUID, blockId: 3, time: 40, inputItems: [{ itemId: 7, count: 5 }], outputItems: [{ itemId: 1, count: 10 }] },
];

export const demoCraftRecipes = {
  recipes: [...craftRecipes.recipes, ...scrollDemoCraftRecipes],
} satisfies CraftRecipesData;

export const demoMachineRecipes = {
  recipes: [...machineRecipes.recipes, ...scrollDemoMachineRecipes],
} satisfies MachineRecipesData;

export const itemList = { itemIds: [100, 101, 102, 1, 2] } satisfies RecipeViewerItemListData;
