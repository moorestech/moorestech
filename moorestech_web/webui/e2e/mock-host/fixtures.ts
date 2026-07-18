import type {
  GameStateData,
  TutorialPresentationData,
  SkitPresentationData,
  PlayerInventoryData,
  CraftRecipesData,
  MachineRecipesData,
  RecipeViewerItemListData,
  ItemMasterData,
  BlockInventoryData,
  ModalRequest,
  ProgressData,
  UiStateData,
  BuildMenuData,
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
      craftTime: 0.2,
      requiredItems: [
        { itemId: 1, count: 2 },
        { itemId: 2, count: 1 },
      ],
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

export const machineRecipes = { recipes: [] } satisfies MachineRecipesData;

export const itemList = { itemIds: [100, 101, 1, 2] } satisfies RecipeViewerItemListData;

// INFRA-6: 既定はインベントリ画面（既存 e2e が前提とする表示状態を保つ）
// INFRA-6: default to the inventory screen (keeps the visibility existing e2e tests assume)
export const uiState = { state: "PlayerInventory" } satisfies UiStateData;

export const itemMaster = {
  items: [
    { itemId: 1, name: "Wood", maxStack: 100 },
    { itemId: 2, name: "Stone", maxStack: 100 },
    { itemId: 100, name: "Plank", maxStack: 100 },
    { itemId: 101, name: "Impossible Plank", maxStack: 100 },
  ],
} satisfies ItemMasterData;

export const buildMenu = {
  entries: [
    { entryType: "block", entryKey: "wood-chest", label: "木箱", tooltip: "木箱を建築" },
    { entryType: "trainCar", entryKey: "cargo-car", label: "貨車", tooltip: "貨車を建築" },
    { entryType: "blueprint", entryKey: "starter-base", label: "拠点BP", tooltip: "保存済み設計図" },
  ],
} satisfies BuildMenuData;

// DEMO(採点用): 60件=10段分。可視7段+スクロール余剰でノブ比が正本(≈70%)と揃う
// DEMO (scoring): 60 items = 10 rows; 7 visible + overflow puts the thumb ratio at the reference's ~70%
export const demoItemList = { itemIds: [100, ...Array.from({ length: 59 }, (_, i) => i + 1)] } satisfies RecipeViewerItemListData;

// 正本スクショと同じ充填パターン（1段目6・2段目3・3段目空・4段目末尾のみ・5-6段目12・ホットバー9）
// Mirror the reference screenshot fill pattern (row1 x6, row2 x3, row3 empty, row4 last only, rows5-6 x12, hotbar x9)
export const demoInventory = {
  mainSlots: [
    ...[100, 100, 100, 27, 3, 62].map((count, i) => ({ itemId: i + 3, count })),
    { itemId: 9, count: 3 },
    { itemId: 10, count: 2 },
    { itemId: 11, count: 52 },
    ...Array.from({ length: 14 }, empty),
    { itemId: 12, count: 35 },
    ...[63, 100, 100, 100, 100, 100, 100, 100, 53, 23, 11, 100].map((count, i) => ({ itemId: (i % 8) + 13, count })),
  ],
  hotbarSlots: [
    { itemId: 2, count: 100 },
    // hue=(itemId*47)%360が青緑域(160-290)のIDを避ける（選択枠のシアン検出を汚染しないため）
    // Avoid ids whose hue lands in cyan-blue (160-290) so they don't pollute cyan ring detection
    ...[100, 100, 92, 100, 100, 32, 100, 8].map((count, i) => ({ itemId: [23, 24, 16, 22, 15, 17, 18, 14][i], count })),
  ],
  grab: empty(),
  selectedHotbar: 0,
} satisfies PlayerInventoryData;

export const demoItemMaster = {
  items: Array.from({ length: 120 }, (_, i) => ({ itemId: i + 1, name: `Item ${i + 1}`, maxStack: 100 })),
} satisfies ItemMasterData;

// DEMO: 進捗バー非表示でホットバーをすっきり見せる
// DEMO: hide the progress bar to keep the hotbar clean
export const demoProgress = { visible: false, progress: 0 } satisfies ProgressData;

// チャレンジのe2e最小fixture（1カテゴリ2ノード・進行1件）
// Minimal challenge fixtures for e2e: one category with two nodes and one active challenge
export const challengeTree = {
  categories: [
    {
      guid: "cat-1",
      name: "Basics",
      iconItemId: 1,
      nodes: [
        { guid: "ch-1", title: "First Craft", summary: "craft something", iconItemId: 1, state: "completed", position: { x: 0, y: 0 }, scale: { x: 1, y: 1 }, prevGuids: [] },
        { guid: "ch-2", title: "Second Step", summary: "keep going", iconItemId: 2, state: "current", position: { x: 220, y: 0 }, scale: { x: 1, y: 1 }, prevGuids: ["ch-1"] },
      ],
    },
  ],
};
export const challengeCurrent = { challenges: [{ guid: "ch-2", title: "Second Step", categoryGuid: "cat-1" }], completedChallengeGuid: null };

export const gameState = { state: "InGame" } satisfies GameStateData;
export const tutorialPresentation = {
  tutorialSessionId: "", revision: 0, challengeId: "", highlights: [],
} satisfies TutorialPresentationData;
export const skitPresentation = {
  sessionId: "", sceneRevision: 0,
  presentationState: {
    mode: "none", speakerName: "", body: "", choices: [], textAreaVisible: false,
    transitionVisible: false, autoEnabled: false, skipActive: false, uiHidden: false,
    textReveal: { mode: "instant", intervalMs: 0 },
  },
  allowedIntents: [],
} satisfies SkitPresentationData;
