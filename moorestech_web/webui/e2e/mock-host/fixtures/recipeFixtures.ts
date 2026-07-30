// クラフト・機械レシピとレシピビューア用アイテム一覧のモックsnapshot
// Mock snapshots for crafting, machine recipes, and the recipe viewer item list
import type { CraftRecipesData, MachineRecipesData, RecipeViewerItemListData } from "../../../src/bridge/contract/payloadTypes";
import { ELECTRIC_MACHINE_BLOCK_GUID, GEAR_MACHINE_BLOCK_GUID } from "./blockLocalizationFixtures";

export const craftRecipes = {
  recipes: [
    {
      recipeGuid: "g-craft-1",
      resultItemId: 100,
      resultCount: 1,
      craftTime: 0.2,
      requiredItems: [{ itemId: 1, count: 2 }, { itemId: 2, count: 1 }],
    },
    {
      recipeGuid: "g-craft-insufficient",
      resultItemId: 101,
      resultCount: 1,
      craftTime: 0.2,
      requiredItems: [{ itemId: 1, count: 999 }],
    },
  ],
} satisfies CraftRecipesData;

export const machineRecipes = {
  recipes: [
    {
      recipeGuid: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
      blockGuid: ELECTRIC_MACHINE_BLOCK_GUID,
      blockId: 3, time: 5,
      inputItems: [{ itemId: 1, count: 2 }], outputItems: [{ itemId: 3, count: 1 }],
    },
    {
      recipeGuid: "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
      blockGuid: ELECTRIC_MACHINE_BLOCK_GUID,
      blockId: 3, time: 10,
      inputItems: [{ itemId: 2, count: 3 }], outputItems: [{ itemId: 7, count: 2 }],
    },
    {
      recipeGuid: "cccccccc-cccc-cccc-cccc-cccccccccccc",
      blockGuid: ELECTRIC_MACHINE_BLOCK_GUID,
      blockId: 3, time: 15,
      inputItems: [{ itemId: 1, count: 1 }, { itemId: 2, count: 1 }], outputItems: [{ itemId: 11, count: 4 }],
    },
    {
      recipeGuid: "dddddddd-dddd-dddd-dddd-dddddddddddd",
      blockGuid: GEAR_MACHINE_BLOCK_GUID,
      blockId: 4, time: 20,
      inputItems: [{ itemId: 3, count: 2 }], outputItems: [{ itemId: 7, count: 1 }],
    },
  ],
} satisfies MachineRecipesData;

export const itemList = { itemIds: [100, 101, 1, 2] } satisfies RecipeViewerItemListData;
