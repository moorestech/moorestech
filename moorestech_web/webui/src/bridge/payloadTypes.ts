// サーバー由来の DTO 型（topic snapshot / event の data 部）
// Server-originated DTO types (the data part of topic snapshot/event)
// 現状は手書き。将来 C# からの自動生成に置換する余地を残す
// Handwritten for now; leaves room to be replaced by C#-generated types later

export type SlotData = { itemId: number; count: number };
export type InventoryArea = "main" | "hotbar" | "grab";
export type SlotRef = { area: InventoryArea; slot: number };

export type PlayerInventoryData = {
  mainSlots: SlotData[];
  hotbarSlots: SlotData[];
  grab: SlotData;
};

export type RequiredItem = { itemId: number; count: number };
export type CraftRecipe = {
  recipeGuid: string;
  resultItemId: number;
  resultCount: number;
  craftTime: number;
  requiredItems: RequiredItem[];
};
export type CraftRecipesData = { recipes: CraftRecipe[] };

export type MachineRecipeItem = { itemId: number; count: number };
export type MachineRecipe = {
  recipeGuid: string;
  blockItemId: number;
  blockName: string;
  time: number;
  inputItems: MachineRecipeItem[];
  outputItems: MachineRecipeItem[];
};
export type MachineRecipesData = { recipes: MachineRecipe[] };

export type RecipeViewerItemListData = { itemIds: number[] };

export type ItemMasterEntry = { itemId: number; name: string; maxStack: number };
export type ItemMasterData = { items: ItemMasterEntry[] };
