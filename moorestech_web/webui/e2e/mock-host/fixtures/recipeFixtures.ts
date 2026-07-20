// クラフト・機械レシピとレシピビューア用アイテム一覧のモックsnapshot
// Mock snapshots for crafting, machine recipes, and the recipe viewer item list
import type { CraftRecipesData, MachineRecipesData, RecipeViewerItemListData } from "../../../src/bridge/contract/payloadTypes";

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
      blockGuid: "11111111-1111-1111-1111-111111111111",
      blockId: 3, blockName: "電気機械", time: 5,
      inputItems: [{ itemId: 1, count: 2 }], outputItems: [{ itemId: 3, count: 1 }],
    },
    {
      recipeGuid: "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
      blockGuid: "11111111-1111-1111-1111-111111111111",
      blockId: 3, blockName: "電気機械", time: 10,
      inputItems: [{ itemId: 2, count: 3 }], outputItems: [{ itemId: 7, count: 2 }],
    },
    {
      recipeGuid: "cccccccc-cccc-cccc-cccc-cccccccccccc",
      blockGuid: "11111111-1111-1111-1111-111111111111",
      blockId: 3, blockName: "電気機械", time: 15,
      inputItems: [{ itemId: 1, count: 1 }, { itemId: 2, count: 1 }], outputItems: [{ itemId: 11, count: 4 }],
    },
    {
      recipeGuid: "dddddddd-dddd-dddd-dddd-dddddddddddd",
      blockGuid: "22222222-2222-2222-2222-222222222222",
      blockId: 4, blockName: "ギア機械", time: 20,
      inputItems: [{ itemId: 3, count: 2 }], outputItems: [{ itemId: 7, count: 1 }],
    },
  ],
} satisfies MachineRecipesData;

export const itemList = { itemIds: [100, 101, 1, 2] } satisfies RecipeViewerItemListData;
