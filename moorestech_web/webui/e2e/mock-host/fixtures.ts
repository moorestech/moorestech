import type {
  PlayerInventoryData,
  SkitPresentationData,
  RecipeViewerItemListData,
  BlockInventoryWireData,
  ModalRequest,
  ProgressData,
  UiStateData,
  TrainRidingData,
} from "../../src/bridge/contract/payloadTypes";
import { CHEST_BLOCK_GUID, TANK_BLOCK_GUID } from "./fixtures/blockLocalizationFixtures";
import { WATER_FLUID_GUID } from "./fixtures/contentLocalizationFixtures";

// BLK-2〜5/8 詳細ブロックと FEAT-RES-1 研究ツリー、ビルドメニューは別ファイルへ分割し再エクスポートする（200行制約）
// Split the BLK-2..5/8 detail blocks, the FEAT-RES-1 research tree, and the build menu into separate files and re-export (200-line limit)
export * from "./blockDetailFixtures";
export * from "./researchFixtures";
export * from "./fixtures/presentationFixtures";
export * from "./fixtures/recipeFixtures";
export * from "./fixtures/itemMasterFixtures";
export * from "./fixtures/fluidMasterFixtures";
export * from "./fixtures/blockLocalizationFixtures";
export * from "./fixtures/contentLocalizationFixtures";
export * from "./fixtures/buildMenuFixtures";
export * from "./fixtures/hotbarFixtures";

const empty = () => ({ itemId: 0, count: 0 });

// 9x5メインインベントリ(旧行込み)
// 9x5 main inventory (the former hotbar row is now part of it); Wood is split across two slots so collect's consolidation is observable
export const inventory = {
  mainSlots: [
    { itemId: 1, count: 10 },
    { itemId: 2, count: 10 },
    { itemId: 1, count: 5 },
    ...Array.from({ length: 33 }, empty),
    { itemId: 2, count: 3 },
    ...Array.from({ length: 8 }, empty),
  ],
  grab: empty(),
  // 3装備枠、初期選択は素手
  // Three slots, bare hands selected
  equipment: [{ itemId: 1, count: 1 }, ...Array.from({ length: 2 }, empty)],
  selectedEquipment: -1,
  equipmentSelectionConfirmationRevision: 0,
} satisfies PlayerInventoryData;

// 全9列×5行の各行に1つ以上アイテムが載る持ち物。層序specが通知と必ず重なる不透明スロットを得るために使う
// An inventory with at least one item per row of the 9x5 grid, so the layering spec always finds an opaque slot overlapping the notification
export const inventoryEveryRowFilled = {
  ...inventory,
  mainSlots: Array.from({ length: 45 }, (_, i) => (i % 9 === 0 ? { itemId: 1, count: 10 } : empty())),
} satisfies PlayerInventoryData;

// BLK-1 チェスト: 9 スロット(uGUI IChestParam.ItemSlotCount 相当)、一部にアイテム
// BLK-1 chest: 9 slots (mirrors uGUI IChestParam.ItemSlotCount), some filled
export const blockChest = {
  open: true,
  source: "block",
  // blockType は実マスタ値(PascalCase)に合わせる。web レジストリも "Chest" で解決する
  // blockType matches the real master value (PascalCase); the web registry resolves "Chest"
  blockType: "Chest",
  identifier: "block:1",
  blockGuid: CHEST_BLOCK_GUID,
  itemSlots: [{ itemId: 1, count: 7 }, { itemId: 2, count: 4 }, ...Array.from({ length: 7 }, empty)],
  fluidSlots: [],
} satisfies BlockInventoryWireData;

// INV-6 タンク機械: 液体スロット + 製作進捗(ProgressArrowBar 用)
// INV-6 tank machine: fluid slots + processing progress (for ProgressArrowBar)
export const blockTank = {
  open: true,
  source: "block",
  blockType: "tank",
  identifier: "block:2",
  blockGuid: TANK_BLOCK_GUID,
  itemSlots: [],
  fluidSlots: [
    { fluidId: 10, amount: 500, capacity: 1000, fluidGuid: WATER_FLUID_GUID },
    { fluidId: 0, amount: 0, capacity: 1000, fluidGuid: "" },
  ],
  progress: 0.5,
} satisfies BlockInventoryWireData;

// 閉状態は本番ワイヤ同様 open:false のみ（他キーは C# 側で省略される）
// Closed matches the production wire: only open:false (the C# side omits every other key)
export const blockClosed = {
  open: false,
} satisfies BlockInventoryWireData;

export const trainCargo = {
  open: true,
  source: "train",
  blockType: "Train",
  identifier: "train:101",
  itemSlots: [{ itemId: 1, count: 24 }, { itemId: 2, count: 8 }, ...Array.from({ length: 7 }, empty)],
  fluidSlots: [],
} satisfies BlockInventoryWireData;

export const trainContainerMissing = {
  open: true,
  source: "train",
  blockType: "Train",
  identifier: "train:102",
  itemSlots: [],
  fluidSlots: [],
  error: "containerMissing",
} satisfies BlockInventoryWireData;

// 新topicの既定snapshotは必ず非乗車にし、再接続復元でHUDを残留させない
// The new topic always defaults to not riding so reconnect restoration cannot retain the HUD.
export const trainRiding = {
  riding: false,
  branchCandidateCount: 0,
  selectedBranchIndex: 0,
} satisfies TrainRidingData;

// COM-2 モーダル: 確認ダイアログのサンプル
// COM-2 modal: sample confirm dialog
export const modalSample = {
  id: "m1",
  title: "確認",
  message: "これは確認ダイアログです",
  buttonText: "OK",
  variant: "confirm",
} satisfies ModalRequest;

// 進捗HUDは用途別scenarioで明示表示し、テスト間の状態漏れを防ぐ
// Show progress HUDs through explicit scenarios so their state cannot leak between tests
export const progressSample = {
  visible: false,
  progress: 0,
} satisfies ProgressData;

// INFRA-6: 既定はインベントリ画面（既存 e2e が前提とする表示状態を保つ）
// INFRA-6: default to the inventory screen (keeps the visibility existing e2e tests assume)
export const uiState = { state: "PlayerInventory" } satisfies UiStateData;

// DEMO(採点用): 60件=10段分。可視7段+スクロール余剰でノブ比が正本(≈70%)と揃う
// DEMO (scoring): 60 items = 10 rows; 7 visible + overflow puts the thumb ratio at the reference's ~70%
export const demoItemList = { itemIds: [100, ...Array.from({ length: 59 }, (_, i) => i + 1)] } satisfies RecipeViewerItemListData;

// 正本スクショと同じ充填パターン
// Mirror the reference screenshot fill pattern (row1 x6, row2 x3, row3 empty, row4 last only, rows5-6 x12, former hotbar row x9)
export const demoInventory = {
  mainSlots: [
    ...[100, 100, 100, 27, 3, 62].map((count, i) => ({ itemId: i + 3, count })),
    { itemId: 9, count: 3 },
    { itemId: 10, count: 2 },
    { itemId: 11, count: 52 },
    ...Array.from({ length: 14 }, empty),
    { itemId: 12, count: 35 },
    ...[63, 100, 100, 100, 100, 100, 100, 100, 53, 23, 11, 100].map((count, i) => ({ itemId: (i % 8) + 13, count })),
    { itemId: 2, count: 100 },
    // hue=(itemId*47)%360が青緑域(160-290)のIDを避ける（選択枠のシアン検出を汚染しないため）
    // Avoid ids whose hue lands in cyan-blue (160-290) so they don't pollute cyan ring detection
    ...[100, 100, 92, 100, 100, 32, 100, 8].map((count, i) => ({ itemId: [23, 24, 16, 22, 15, 17, 18, 14][i], count })),
  ],
  grab: empty(),
  // 装備HUDが写るIDで充填
  // Fill with the same non-cyan ids as the former hotbar row so the equipment HUD shows at the right edge in scoring screenshots
  equipment: [{ itemId: 23, count: 100 }, { itemId: 24, count: 100 }, empty()],
  selectedEquipment: 0,
  equipmentSelectionConfirmationRevision: 0,
} satisfies PlayerInventoryData;

// DEMO: 進捗バー非表示でホットバーをすっきり見せる
// DEMO: hide the progress bar to keep the hotbar clean
export const demoProgress = { visible: false, progress: 0 } satisfies ProgressData;

export const blockingSkitText = {
  sessionId: "blocking-1", sceneRevision: 1,
  presentationState: {
    mode: "blocking", speakerName: "Moore", body: "Blocking message", choices: [], textAreaVisible: true,
    transitionVisible: false, autoEnabled: false, skipActive: false, uiHidden: false,
    textReveal: { mode: "typewriter", intervalMs: 1000 },
  },
  allowedIntents: ["advance", "set-auto", "skip", "set-ui-hidden"],
} satisfies SkitPresentationData;

export const blockingSkitChoices = {
  sessionId: "blocking-1", sceneRevision: 2,
  presentationState: {
    ...blockingSkitText.presentationState, body: "Choose a route", choices: [
      { choiceId: "route-a", label: "Route A" }, { choiceId: "route-b", label: "Route B" },
    ], textReveal: { mode: "instant", intervalMs: 0 },
  },
  allowedIntents: ["select", "set-auto", "skip", "set-ui-hidden"],
} satisfies SkitPresentationData;
