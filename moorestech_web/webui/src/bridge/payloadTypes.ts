// サーバー由来の DTO 型（topic snapshot / event の data 部）
// Server-originated DTO types (the data part of topic snapshot/event)
// 現状は手書き。将来 C# からの自動生成に置換する余地を残す
// Handwritten for now; leaves room to be replaced by C#-generated types later

export type SlotData = { itemId: number; count: number };
export type InventoryArea = "main" | "hotbar" | "grab";
export type SlotRef = { area: InventoryArea; slot: number };

// ブロックインベントリ移動はプレイヤー側(main/hotbar/grab)とブロック側(block)を跨ぐ
// Block-inventory moves span the player side (main/hotbar/grab) and the block side (block)
export type BlockInventoryArea = InventoryArea | "block";
export type BlockSlotRef = { area: BlockInventoryArea; slot: number };

export type PlayerInventoryData = {
  mainSlots: SlotData[];
  hotbarSlots: SlotData[];
  grab: SlotData;
  // 選択中のホットバー index (0-8)。uGUI HotBarView.SelectIndex 相当
  // Currently selected hotbar index (0-8); mirrors uGUI HotBarView.SelectIndex
  selectedHotbar: number;
};

// COM-2 モーダル要求。uGUI OneButtonModal(title/message/1ボタン) 相当
// COM-2 modal request; mirrors uGUI OneButtonModal (title/message/single button)
export type ModalRequest = {
  id: string;
  title: string;
  message: string;
  buttonText: string;
  variant: "confirm" | "error";
};
export type ModalData = { modal: ModalRequest | null };

// COM-3 汎用プログレス。uGUI ProgressBarView(Show/Hide + 0..1) 相当
// COM-3 generic progress; mirrors uGUI ProgressBarView (Show/Hide + 0..1)
export type ProgressData = { visible: boolean; progress: number; label: string | null };

// INV-6 液体スロット。uGUI FluidSlotView(アイコン + amount/capacity + 名前 tooltip) 相当
// INV-6 fluid slot; mirrors uGUI FluidSlotView (icon + amount/capacity + name tooltip)
export type FluidSlotData = { fluidId: number; amount: number; capacity: number; name: string };

// INV-4/BLK-1 ブロックインベントリ。blockType で React コンポーネントを静的解決
// INV-4/BLK-1 block inventory; blockType statically resolves to a React component
export type BlockInventoryData = {
  open: boolean;
  blockType: string;
  identifier: string;
  blockName: string;
  itemSlots: SlotData[];
  fluidSlots: FluidSlotData[];
  progress: number | null;
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
