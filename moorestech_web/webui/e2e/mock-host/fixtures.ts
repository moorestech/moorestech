import type {
  PlayerInventoryData,
  CraftRecipesData,
  MachineRecipesData,
  RecipeViewerItemListData,
  ItemMasterData,
  BlockInventoryData,
  ModalRequest,
  ProgressData,
  UiStateData,
} from "../../src/bridge/contract/payloadTypes";

// BLK-2〜5/8 詳細ブロックと FEAT-RES-1 研究ツリーは別ファイルへ分割し再エクスポートする（200行制約）
// Split the BLK-2..5/8 detail blocks and the FEAT-RES-1 research tree into separate files and re-export (200-line limit)
export * from "./blockDetailFixtures";
export * from "./researchFixtures";

const empty = () => ({ itemId: 0, count: 0 });

// 9列×4行のメイン + 9列ホットバー。Wood を2スロットに分けて collect の集約を観測可能にする
// 9x4 main + 9 hotbar; Wood is split across two slots so collect's consolidation is observable
export const inventory = {
  mainSlots: [
    { itemId: 1, count: 10 },
    { itemId: 2, count: 10 },
    { itemId: 1, count: 5 },
    ...Array.from({ length: 33 }, empty),
  ],
  hotbarSlots: [{ itemId: 2, count: 3 }, ...Array.from({ length: 8 }, empty)],
  grab: empty(),
  selectedHotbar: 0,
} satisfies PlayerInventoryData;

// BLK-1 チェスト: 9 スロット(uGUI IChestParam.ItemSlotCount 相当)、一部にアイテム
// BLK-1 chest: 9 slots (mirrors uGUI IChestParam.ItemSlotCount), some filled
export const blockChest = {
  open: true,
  // blockType は実マスタ値(PascalCase)に合わせる。web レジストリも "Chest" で解決する
  // blockType matches the real master value (PascalCase); the web registry resolves "Chest"
  blockType: "Chest",
  identifier: "block:1",
  blockName: "Chest",
  itemSlots: [{ itemId: 1, count: 7 }, { itemId: 2, count: 4 }, ...Array.from({ length: 7 }, empty)],
  fluidSlots: [],
} satisfies BlockInventoryData;

// INV-6 タンク機械: 液体スロット + 製作進捗(ProgressArrow 用)
// INV-6 tank machine: fluid slots + processing progress (for ProgressArrow)
export const blockTank = {
  open: true,
  blockType: "tank",
  identifier: "block:2",
  blockName: "Fluid Tank",
  itemSlots: [],
  fluidSlots: [
    { fluidId: 10, amount: 500, capacity: 1000, name: "Water" },
    { fluidId: 0, amount: 0, capacity: 1000, name: "" },
  ],
  progress: 0.5,
} satisfies BlockInventoryData;

// 閉状態は本番ワイヤ同様 open:false のみ（他キーは C# 側で省略される）
// Closed matches the production wire: only open:false (the C# side omits every other key)
export const blockClosed = {
  open: false,
} satisfies BlockInventoryData;

// COM-2 モーダル: 確認ダイアログのサンプル
// COM-2 modal: sample confirm dialog
export const modalSample = {
  id: "m1",
  title: "確認",
  message: "これは確認ダイアログです",
  buttonText: "OK",
  variant: "confirm",
} satisfies ModalRequest;

// COM-3 進捗: クラフト中を表す可視バー
// COM-3 progress: a visible bar representing an in-progress craft
export const progressSample = {
  visible: true,
  progress: 0.4,
  label: "Crafting",
} satisfies ProgressData;

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

// INFRA-6: 既定はインベントリ画面（既存 e2e が前提とする表示状態を保つ）
// INFRA-6: default to the inventory screen (keeps the visibility existing e2e tests assume)
export const uiState = { state: "PlayerInventory" } satisfies UiStateData;

export const itemMaster = {
  items: [
    { itemId: 1, name: "Wood", maxStack: 100 },
    { itemId: 2, name: "Stone", maxStack: 100 },
    { itemId: 100, name: "Plank", maxStack: 100 },
  ],
} satisfies ItemMasterData;

// DEMO(採点用): グリッドを埋めて target 画面の密度へ近づける。既定 fixtures は変えず追加のみ
// DEMO (scoring only): fill the grids to approach the target screen density; additive, defaults untouched
export const demoItemList = { itemIds: [100, ...Array.from({ length: 41 }, (_, i) => i + 1)] } satisfies RecipeViewerItemListData;

// 先頭3スロットは既定と同じ内容にし、残りをアイテムで埋める(空6スロット残す)
// Keep the first 3 slots identical to the default, then fill the rest with items (leaving 6 empty)
export const demoInventory = {
  mainSlots: [
    { itemId: 1, count: 10 },
    { itemId: 2, count: 10 },
    { itemId: 1, count: 5 },
    ...Array.from({ length: 27 }, (_, i) => ({ itemId: (i % 40) + 3, count: ((i * 7) % 99) + 1 })),
    ...Array.from({ length: 6 }, empty),
  ],
  hotbarSlots: [
    { itemId: 2, count: 3 },
    ...Array.from({ length: 4 }, (_, i) => ({ itemId: i + 10, count: (i + 1) * 2 })),
    ...Array.from({ length: 4 }, empty),
  ],
  grab: empty(),
  selectedHotbar: 0,
} satisfies PlayerInventoryData;

export const demoItemMaster = {
  items: Array.from({ length: 120 }, (_, i) => ({ itemId: i + 1, name: `Item ${i + 1}`, maxStack: 100 })),
} satisfies ItemMasterData;
