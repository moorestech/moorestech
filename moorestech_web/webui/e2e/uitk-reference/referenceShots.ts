import { openBlockFixtureNames } from "../mock-host/httpHandler";
import { blockingSkitText, blockingSkitChoices } from "../mock-host/fixtures";
import type { TopicScenario } from "../mock-host/topics/topicControls";

// 撮影単位は束・台帳行・制御・操作
// Each shot groups a bundle, ledger row, controls, and steps

export type ShotStep =
  | { kind: "control"; path: string }
  | { kind: "click"; testId: string }
  | { kind: "rightClickFirstChild"; testId: string }
  | { kind: "revealSkit" }
  | { kind: "expectVisible"; testId: string; descendant: string | null }
  | { kind: "expectContent"; testId: string; texts: string[] }
  | { kind: "wait"; ms: number };

export type ShotSetup = { controls: string[]; steps: ShotStep[] };

// 台帳 docs/uitk/TODO.md の「## 行」の行名。撮影表と台帳を同じ文字列でつなぐ（Step 5 で照合する）
// Row names of the ledger's "## 行" table; the same strings join shots to the ledger (checked in Step 5)
export const LedgerRows = {
  pauseMenu: "pauseMenu（ポーズ・バグ報告・入れ子のポーズ）",
  settings: "settings（画質・言語・ダッシュ方式）",
  toast: "toast",
  commonHud: "commonHud（クロスヘア）",
  modeHud: "modeHud（配置 HUD・削除モードの帯）",
  progress: "progress（採掘ゲージ）",
  trainHud: "trainHud（乗車中の分岐表示）",
  notification: "notification（獲得通知）",
  skit: "skit（会話窓・選択肢・ツールバー・暗転）",
  currentChallengeHud: "challenge: CurrentChallengeHud",
  keyControlHint: "tutorial: KeyControlHintHud",
  worldPin: "tutorial: WorldPinOverlay",
  cursorTooltip: "CursorTooltip",
  backgroundSkit: "オーバーレイ: 背景スキット",
  buildMenu: "buildMenu",
  hotbar: "hotbar",
  modal: "modal（BP 名入力）",
  inventory: "inventory（インベントリ・装備・掴み）",
  recipe: "recipe（レシピ・アイテム一覧・長押しクラフト）",
  blockInventory: "blockInventory（汎用族・特殊族・列車族）",
  research: "research（研究ツリー・詳細ペイン）",
  dragGuide: "tutorial: DragGuide（インベントリ内）",
  challengePanel: "challenge: ChallengePanel",
  eventLanguageGate: "eventLanguageGate（出展モードの言語選択）",
  tutorialOverlay: "tutorial: TutorialOverlay（ハイライト枠）",
  reconnect: "オーバーレイ: 再接続・辞書エラー・エラー境界",
  i18n: "i18n",
} as const;
export type LedgerRow = (typeof LedgerRows)[keyof typeof LedgerRows];
// 束の番号。撮影表・動画表・manifest で同じ型を使う
// Bundle number, shared by the shot table, the video table, and the manifest
export type BundleId = 1 | 2 | 3 | 4 | 5;

type ReferenceShot = ShotSetup & { id: string; bundle: BundleId; ledgerRow: LedgerRow };

export const ui = (state: string, subState?: string) => subState === undefined ? `/__uistate?state=${state}` : `/__uistate?state=${state}&subState=${subState}`;
export const topic = (scenario: TopicScenario) => `/__topic-control?scenario=${scenario}`;
const block = (type: string) => `/__block?type=${type}`;
const after = (path: string): ShotStep => ({ kind: "control", path });

// 表示条件も撮影手順に保持し、空の画面を成功扱いにしない
// Keep visibility requirements with each shot so empty captures fail
export const visible = (testId: string): ShotStep => ({ kind: "expectVisible", testId, descendant: null });
export const content = (testId: string, texts: string[]): ShotStep => ({ kind: "expectContent", testId, texts });

export const referenceShots: ReferenceShot[] = [
  // 束 1 命綱
  { id: "1-pause-top", bundle: 1, ledgerRow: LedgerRows.pauseMenu, controls: [topic("pauseConnected"), ui("PauseMenu")], steps: [visible("pause-menu-open-settings")] },
  { id: "1-pause-disconnected", bundle: 1, ledgerRow: LedgerRows.pauseMenu, controls: [topic("pauseDisconnected"), ui("PauseMenu")], steps: [content("pause-menu", ["サーバーから切断されました"])] },
  { id: "1-pause-delivery-pending", bundle: 1, ledgerRow: LedgerRows.pauseMenu, controls: [topic("deliveryPending"), ui("PauseMenu")], steps: [{ kind: "click", testId: "pause-menu-open-bug-report" }, content("bug-report-delivery", ["送信待ち 3件"])] },
  { id: "1-pause-in-skit", bundle: 1, ledgerRow: LedgerRows.pauseMenu, controls: [ui("Story", "PauseMenuScreen")], steps: [visible("pause-menu-open-settings")] },
  { id: "1-pause-in-train", bundle: 1, ledgerRow: LedgerRows.pauseMenu, controls: [ui("TrainHUDScreen", "PauseMenuScreen")], steps: [visible("pause-menu-open-settings")] },
  { id: "1-bug-report", bundle: 1, ledgerRow: LedgerRows.pauseMenu, controls: [ui("PauseMenu")], steps: [{ kind: "click", testId: "pause-menu-open-bug-report" }, visible("bug-report-description")] },
  { id: "1-settings", bundle: 1, ledgerRow: LedgerRows.settings, controls: [ui("PauseMenu")], steps: [{ kind: "click", testId: "pause-menu-open-settings" }, visible("graphics-preset")] },
  { id: "1-settings-graphics-recovered", bundle: 1, ledgerRow: LedgerRows.settings, controls: [topic("graphicsRecovered"), ui("PauseMenu")], steps: [{ kind: "click", testId: "pause-menu-open-settings" }, visible("graphics-recovered")] },
  { id: "1-toast", bundle: 1, ledgerRow: LedgerRows.toast, controls: [block("chest"), "/__action-error?type=block_inventory.split&error=invalid_slot", ui("SubInventory")],
    steps: [{ kind: "rightClickFirstChild", testId: "chest-grid" }, content("toast-host", ["invalid_slot"])] },
  // 束 2 常駐表示
  { id: "2-game-hud", bundle: 2, ledgerRow: LedgerRows.commonHud, controls: [topic("crosshairVisible"), topic("challengeActive"), ui("GameScreen")], steps: [visible("hotbar-grid"), visible("challenge-hud"), { kind: "expectVisible", testId: "app-stage", descendant: '[data-tutorial-anchor~="game.crosshair"]' }] },
  { id: "2-mining", bundle: 2, ledgerRow: LedgerRows.progress, controls: [topic("mining"), ui("GameScreen")], steps: [visible("progress-bar")] },
  { id: "2-progress-labeled", bundle: 2, ledgerRow: LedgerRows.progress, controls: [topic("progressLabeled"), ui("GameScreen")], steps: [content("progress-bar", ["Crafting"])] },
  { id: "2-placement", bundle: 2, ledgerRow: LedgerRows.modeHud, controls: [topic("placement"), ui("PlaceBlock")], steps: [content("operation-mode-detail", ["Assembler", "3"])] },
  { id: "2-placement-unavailable", bundle: 2, ledgerRow: LedgerRows.modeHud, controls: [topic("placementUnavailable"), ui("PlaceBlock")], steps: [content("operation-mode-warning", ["Blocked by terrain"])] },
  { id: "2-placement-connect-tool", bundle: 2, ledgerRow: LedgerRows.modeHud, controls: [topic("placementConnectTool"), ui("PlaceBlock")], steps: [visible("placement-mode-hud"), visible("operation-mode-detail")] },
  { id: "2-delete-bar", bundle: 2, ledgerRow: LedgerRows.modeHud, controls: [ui("DeleteBar")], steps: [visible("delete-mode-warning")] },
  { id: "2-train-hud", bundle: 2, ledgerRow: LedgerRows.trainHud, controls: ["/__train-riding?riding=1&count=3&selected=1", ui("TrainHUDScreen", "GameScreen")], steps: [visible("train-branch-selection")] },
  { id: "2-notifications", bundle: 2, ledgerRow: LedgerRows.notification, controls: [ui("GameScreen")],
    steps: [visible("hotbar-grid"), after(topic("notificationItemEarned")), after(topic("notificationAchievement")), after(topic("notificationItemUnlocked")), after(topic("notificationDenied")), content("notification-row", ["5", "研究完了", "新しいアイテム", "研究を完了できません"])] },
  { id: "2-tooltip", bundle: 2, ledgerRow: LedgerRows.cursorTooltip, controls: [topic("tooltip"), ui("GameScreen")], steps: [content("cursor-tooltip-line", ["対象"])] },
  { id: "2-tooltip-multiline", bundle: 2, ledgerRow: LedgerRows.cursorTooltip, controls: [topic("tooltipMultiLine"), ui("GameScreen")], steps: [content("cursor-tooltip-line", ["地形に埋まっています", "遠すぎます"])] },
  { id: "2-challenge-hud-multiple-long", bundle: 2, ledgerRow: LedgerRows.currentChallengeHud, controls: [topic("challengeMultipleLong"), ui("GameScreen")], steps: [visible("challenge-objective")] },
  { id: "2-skit-background", bundle: 2, ledgerRow: LedgerRows.backgroundSkit, controls: [ui("GameScreen"), "/__skit?stage=background"], steps: [visible("background-skit")] },
  { id: "2-skit-text", bundle: 2, ledgerRow: LedgerRows.skit, controls: [ui("Story", "GameScreen"), "/__skit?stage=text"], steps: [{ kind: "revealSkit" }, content("blocking-skit", [blockingSkitText.presentationState.body])] },
  { id: "2-skit-choices", bundle: 2, ledgerRow: LedgerRows.skit, controls: [ui("Story", "GameScreen"), "/__skit?stage=choices"], steps: [content("blocking-skit", [blockingSkitChoices.presentationState.body])] },
  { id: "2-skit-transition", bundle: 2, ledgerRow: LedgerRows.skit, controls: [ui("Story", "GameScreen"), "/__skit?stage=transition"], steps: [visible("skit-transition")] },
  { id: "2-world-pin-onscreen", bundle: 2, ledgerRow: LedgerRows.worldPin, controls: [ui("GameScreen"), "/__worldpin?x=0.6&y=0.4"], steps: [visible("world-pin-map-object-pin")] },
  { id: "2-world-pin-offscreen", bundle: 2, ledgerRow: LedgerRows.worldPin, controls: [ui("GameScreen"), "/__worldpin?on=0&dx=1&dy=0.3"], steps: [visible("world-pin-arrow-map-object-pin")] },
  { id: "2-key-control-hint", bundle: 2, ledgerRow: LedgerRows.keyControlHint, controls: [topic("tutorialKeyControl"), ui("GameScreen")], steps: [visible("key-control-hint")] },
  // 束 3 建築
  { id: "3-build-menu", bundle: 3, ledgerRow: LedgerRows.buildMenu, controls: [ui("BuildMenu")], steps: [visible("build-menu-columns")] },
  { id: "3-hotbar-equipment-empty", bundle: 3, ledgerRow: LedgerRows.hotbar, controls: [topic("equipmentEmpty"), ui("GameScreen")], steps: [visible("hotbar-grid")] },
  { id: "3-modal", bundle: 3, ledgerRow: LedgerRows.modal, controls: [ui("GameScreen"), "/__modal?show=1&kind=blueprint"], steps: [visible("modal-input")] },
  // 束 4 持ち物
  { id: "4-player-inventory", bundle: 4, ledgerRow: LedgerRows.inventory, controls: [ui("PlayerInventory")], steps: [visible("main-grid"), visible("item-list-grid")] },
  { id: "4-player-inventory-full", bundle: 4, ledgerRow: LedgerRows.inventory, controls: [topic("inventoryEveryRowFilled"), ui("PlayerInventory")], steps: [visible("main-grid"), visible("equipment-slots")] },
  { id: "4-item-list-large", bundle: 4, ledgerRow: LedgerRows.recipe, controls: [topic("itemListLarge"), ui("PlayerInventory")], steps: [visible("item-list-grid")] },
  ...openBlockFixtureNames.map((type): ReferenceShot => ({ id: `4-block-${type}`, bundle: 4, ledgerRow: LedgerRows.blockInventory, controls: [block(type), ui("SubInventory")], steps: [visible("block-inventory")] })),
  { id: "4-block-train-timetable-tab", bundle: 4, ledgerRow: LedgerRows.blockInventory, controls: [block("trainTimetable"), ui("SubInventory")], steps: [{ kind: "click", testId: "train-tab-timetable" }, visible("train-timetable-section")] },
  { id: "4-block-machine-recipe-selection", bundle: 4, ledgerRow: LedgerRows.blockInventory, controls: [topic("machineRecipesOverflow"), block("machine"), ui("SubInventory")], steps: [{ kind: "click", testId: "machine-selected-recipe" }, visible("machine-recipe-selection")] },
  { id: "4-research", bundle: 4, ledgerRow: LedgerRows.research, controls: [topic("researchOwnedItems"), ui("ResearchTree")], steps: [visible("research-tree")] },
  // 束 5 最後
  { id: "5-challenge-list", bundle: 5, ledgerRow: LedgerRows.challengePanel, controls: [topic("challengeActive"), ui("ChallengeList")], steps: [visible("challenge-panel")] },
  { id: "5-event-language-gate", bundle: 5, ledgerRow: LedgerRows.eventLanguageGate, controls: [topic("eventLanguageGateWaiting"), ui("GameScreen")], steps: [visible("event-language-gate")] },
  { id: "5-tutorial-outline-label", bundle: 5, ledgerRow: LedgerRows.tutorialOverlay, controls: [topic("tutorialOutlineWithLabel"), ui("GameScreen")], steps: [visible("tutorial-highlight-label")] },
  { id: "5-tutorial-recipe-item", bundle: 5, ledgerRow: LedgerRows.tutorialOverlay, controls: [topic("tutorialRecipeItem"), ui("PlayerInventory")], steps: [visible("tutorial-highlight-label")] },
  { id: "5-tutorial-research-node", bundle: 5, ledgerRow: LedgerRows.tutorialOverlay, controls: [topic("tutorialResearchNode"), ui("ResearchTree")], steps: [{ kind: "expectVisible", testId: "tutorial-overlay", descendant: '[data-kind="outline"]' }] },
  { id: "5-reconnect", bundle: 5, ledgerRow: LedgerRows.reconnect, controls: [ui("GameScreen")],
    steps: [after("/__disconnect?holdMs=8000"), visible("reconnect-overlay")] },
  { id: "5-japanese-inventory", bundle: 5, ledgerRow: LedgerRows.i18n, controls: [topic("japanese"), ui("PlayerInventory")], steps: [visible("main-grid")] },
];
