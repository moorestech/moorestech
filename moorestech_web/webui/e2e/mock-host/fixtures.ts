import type {
  PlayerInventoryData,
  SkitPresentationData,
  RecipeViewerItemListData,
  BlockInventoryData,
  ModalRequest,
  ProgressData,
  UiStateData,
  BuildMenuData,
  TrainRidingData,
} from "../../src/bridge/contract/payloadTypes";
import {
  CHEST_BLOCK_GUID,
  TANK_BLOCK_GUID,
} from "./fixtures/blockLocalizationFixtures";

// BLK-2〜5/8 詳細ブロックと FEAT-RES-1 研究ツリーは別ファイルへ分割し再エクスポートする（200行制約）
// Split the BLK-2..5/8 detail blocks and the FEAT-RES-1 research tree into separate files and re-export (200-line limit)
export * from "./blockDetailFixtures";
export * from "./researchFixtures";
export * from "./fixtures/presentationFixtures";
export * from "./fixtures/recipeFixtures";
export * from "./fixtures/itemMasterFixtures";
export * from "./fixtures/blockLocalizationFixtures";
export * from "./fixtures/contentLocalizationFixtures";

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
  source: "block",
  // blockType は実マスタ値(PascalCase)に合わせる。web レジストリも "Chest" で解決する
  // blockType matches the real master value (PascalCase); the web registry resolves "Chest"
  blockType: "Chest",
  identifier: "block:1",
  blockGuid: CHEST_BLOCK_GUID,
  itemSlots: [{ itemId: 1, count: 7 }, { itemId: 2, count: 4 }, ...Array.from({ length: 7 }, empty)],
  fluidSlots: [],
} satisfies BlockInventoryData;

// INV-6 タンク機械: 液体スロット + 製作進捗(ProgressArrow 用)
// INV-6 tank machine: fluid slots + processing progress (for ProgressArrow)
export const blockTank = {
  open: true,
  source: "block",
  blockType: "tank",
  identifier: "block:2",
  blockGuid: TANK_BLOCK_GUID,
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

export const trainCargo = {
  open: true,
  source: "train",
  blockType: "Train",
  identifier: "train:101",
  itemSlots: [{ itemId: 1, count: 24 }, { itemId: 2, count: 8 }, ...Array.from({ length: 7 }, empty)],
  fluidSlots: [],
} satisfies BlockInventoryData;

export const trainContainerMissing = {
  open: true,
  source: "train",
  blockType: "Train",
  identifier: "train:102",
  itemSlots: [],
  fluidSlots: [],
  error: "containerMissing",
} satisfies BlockInventoryData;

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

// カテゴリ×サブカテゴリ構成。「鉄」検索で 物流/チェスト と 輸送/鉄道 が複数カテゴリ跨ぎでヒットする
// Category x sub-category layout; searching "鉄" hits both 物流/チェスト and 輸送/鉄道 across categories
export const buildMenu = {
  categories: [
    { categoryGuid: "51000000-0000-4000-8000-000000000001", subCategoryGuids: ["52000000-0000-4000-8000-000000000001", "52000000-0000-4000-8000-000000000002"] },
    { categoryGuid: "51000000-0000-4000-8000-000000000002", subCategoryGuids: ["52000000-0000-4000-8000-000000000003", "52000000-0000-4000-8000-000000000004"] },
    { categoryGuid: "51000000-0000-4000-8000-000000000003", subCategoryGuids: ["52000000-0000-4000-8000-000000000005"] },
    // エントリを持たない空カテゴリ。サイドバーの除外分岐を検証するためのもの
    // An empty category with no entries, to exercise the sidebar's exclusion branch
    { categoryGuid: "51000000-0000-4000-8000-000000000004", subCategoryGuids: ["52000000-0000-4000-8000-000000000006"] },
  ],
  entries: [
    { entryType: "block", entryKey: "53000000-0000-4000-8000-000000000001", categoryGuid: "51000000-0000-4000-8000-000000000001", subCategoryGuid: "52000000-0000-4000-8000-000000000001", requiredItems: [{ itemId: 1, count: 4 }], iconUrl: "/icons/wood-chest.png" },
    { entryType: "block", entryKey: "53000000-0000-4000-8000-000000000002", categoryGuid: "51000000-0000-4000-8000-000000000001", subCategoryGuid: "52000000-0000-4000-8000-000000000001", requiredItems: [], iconUrl: "/icons/iron-chest.png" },
    { entryType: "block", entryKey: "53000000-0000-4000-8000-000000000003", categoryGuid: "51000000-0000-4000-8000-000000000001", subCategoryGuid: "52000000-0000-4000-8000-000000000002", requiredItems: [], iconUrl: "/icons/belt-conveyor.png" },
    { entryType: "block", entryKey: "53000000-0000-4000-8000-000000000004", categoryGuid: "51000000-0000-4000-8000-000000000002", subCategoryGuid: "52000000-0000-4000-8000-000000000003", requiredItems: [], iconUrl: "/icons/rail.png" },
    { entryType: "trainCar", entryKey: "54000000-0000-4000-8000-000000000001", label: "貨物車両", categoryGuid: "51000000-0000-4000-8000-000000000002", subCategoryGuid: "52000000-0000-4000-8000-000000000004", requiredItems: [], iconUrl: "/icons/cargo-car.png" },
    { entryType: "blueprintCopy", entryKey: "", categoryGuid: "51000000-0000-4000-8000-000000000003", subCategoryGuid: "52000000-0000-4000-8000-000000000005", requiredItems: [] },
    { entryType: "blueprint", entryKey: "starter-base", label: "starter-base", categoryGuid: "51000000-0000-4000-8000-000000000003", subCategoryGuid: "52000000-0000-4000-8000-000000000005", requiredItems: [] },
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
