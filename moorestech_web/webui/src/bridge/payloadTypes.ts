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
// modal は C# 側 NullValueHandling.Ignore で null 時にキーごと省略される（無し時は {}）
// modal is dropped key-and-all when null by the C# NullValueHandling.Ignore (absent → {})
export type ModalData = { modal?: ModalRequest };

// COM-3 汎用プログレス。uGUI ProgressBarView(Show/Hide + 0..1) 相当
// COM-3 generic progress; mirrors uGUI ProgressBarView (Show/Hide + 0..1)
// label は null 時にキー省略されるため optional（型で欠落を表現する）
// label is key-omitted when null, so it is optional (the type expresses the omission)
export type ProgressData = { visible: boolean; progress: number; label?: string };

// INV-6 液体スロット。uGUI FluidSlotView(アイコン + amount/capacity + 名前 tooltip) 相当
// INV-6 fluid slot; mirrors uGUI FluidSlotView (icon + amount/capacity + name tooltip)
export type FluidSlotData = { fluidId: number; amount: number; capacity: number; name: string };

// INV-4/BLK-1 ブロックインベントリ。閉状態は open:false のみ、他キーは C# 側で全省略される
// INV-4/BLK-1 block inventory; the closed state is only open:false, the C# side omits every other key
// open を判別子にした discriminated union で「開なら全フィールド存在」を型が保証する（!data.open ガードを正当化）
// A discriminated union on open lets the type guarantee "all fields present when open" (justifies the !data.open guard)
export type BlockInventoryOpen = {
  open: true;
  blockType: string;
  identifier: string;
  blockName: string;
  itemSlots: SlotData[];
  fluidSlots: FluidSlotData[];
  // progress は null 時にキー省略されるため optional
  // progress is key-omitted when null, so it is optional
  progress?: number;
};
export type BlockInventoryClosed = { open: false };
export type BlockInventoryData = BlockInventoryOpen | BlockInventoryClosed;

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
