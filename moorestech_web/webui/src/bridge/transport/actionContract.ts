import type { SlotRef, BlockSlotRef, BuildMenuEntryType } from "../contract/payloadTypes";

// C# UIStateEnum 由来の state 名。文字列リテラルの散在を防ぐ
// State names from the C# UIStateEnum; prevents scattered string literals
export const UiStateNames = {
  gameScreen: "GameScreen",
  playerInventory: "PlayerInventory",
  subInventory: "SubInventory",
  researchTree: "ResearchTree",
  buildMenu: "BuildMenu",
  challengeList: "ChallengeList",
  pauseMenu: "PauseMenu",
  placeBlock: "PlaceBlock",
  deleteBar: "DeleteBar",
  trainHud: "TrainHUDScreen",
} as const;

// action type → payload 型の対応表。dispatchAction がこれで型付けされる
// action type → payload type registry; types dispatchAction
export type ActionPayloads = {
  "inventory.move_item": { from: SlotRef; to: SlotRef; count: number };
  "inventory.split": { from: SlotRef };
  "inventory.collect": { slot: SlotRef };
  "inventory.split_drag": { slots: SlotRef[] };
  "inventory.sort": Record<string, never>;
  "inventory.select_hotbar": { index: number };
  "craft.execute": { recipeGuid: string };
  // text は input モーダルの確定時のみ付与する
  // text accompanies only the confirm of an input modal
  "ui.modal.respond": { id: string; result: "confirm" | "cancel"; text?: string };
  "build_menu.select": { entryType: BuildMenuEntryType; entryKey: string };
  "blueprint.delete": { name: string };
  "block_inventory.move_item": { from: BlockSlotRef; to: BlockSlotRef; count: number };
  "block_inventory.split": { from: BlockSlotRef };
  "block_inventory.collect": { slot: BlockSlotRef };
  "ui_state.request": { state: typeof UiStateNames.gameScreen | typeof UiStateNames.playerInventory };
  "pause_menu.save": Record<string, never>;
  "pause_menu.back_to_main_menu": Record<string, never>;
  "research.complete": { researchGuid: string };
  "machine_recipe.select": { operation: "set" | "clear"; recipeGuid?: string };
  "filter_splitter.set_mode": { directionIndex: number; mode: "default" | "whitelist" | "blacklist" };
  // clear:true は右クリック相当のフィルタ解除。clear:false は C# 側が Grab の持ち手アイテムを設定する
  // clear:true clears the filter (right-click); with clear:false the C# side assigns the currently grabbed item
  "filter_splitter.set_filter_item": { directionIndex: number; slotIndex: number; clear: boolean };
  "electric_to_gear.set_output_mode": { modeIndex: number };
  "train_platform.set_transfer_mode": { mode: "loadToTrain" | "unloadToPlatform" };
  "debug.echo": { hello: string };
  "tutorial.anchor_ack": {
    tutorialSessionId: string; revision: number; highlightId: string; anchorId: string;
    status: "ready" | "not-found" | "hidden";
    reason: "mounted" | "missing" | "duplicate-anchor" | "display-none" | "visibility-hidden" | "aria-hidden" | "zero-area" | "outside-viewport";
  };
  "skit.advance": { sessionId: string; sceneRevision: number };
  "skit.select": { sessionId: string; sceneRevision: number; choiceId: string };
  "skit.set_auto": { sessionId: string; sceneRevision: number; enabled: boolean };
  "skit.skip": { sessionId: string; sceneRevision: number };
  "skit.set_ui_hidden": { sessionId: string; sceneRevision: number; hidden: boolean };
};

// 既知 action type の実行時リスト。ActionPayloads のキーと1:1（下の網羅チェックで担保）
// Runtime list of known action types, 1:1 with ActionPayloads keys (enforced by the check below)
export const ACTION_TYPES = [
  "inventory.move_item",
  "inventory.split",
  "inventory.collect",
  "inventory.split_drag",
  "inventory.sort",
  "inventory.select_hotbar",
  "craft.execute",
  "ui.modal.respond",
  "build_menu.select",
  "blueprint.delete",
  "block_inventory.move_item",
  "block_inventory.split",
  "block_inventory.collect",
  "ui_state.request",
  "pause_menu.save",
  "pause_menu.back_to_main_menu",
  "research.complete",
  "machine_recipe.select",
  "filter_splitter.set_mode",
  "filter_splitter.set_filter_item",
  "electric_to_gear.set_output_mode",
  "train_platform.set_transfer_mode",
  "debug.echo",
  "tutorial.anchor_ack",
  "skit.advance",
  "skit.select",
  "skit.set_auto",
  "skit.skip",
  "skit.set_ui_hidden",
] as const satisfies readonly (keyof ActionPayloads)[];

export type ActionType = (typeof ACTION_TYPES)[number];

// ActionPayloads にあって ACTION_TYPES に無いキーがあると never 制約違反でコンパイルエラーになる
// A key in ActionPayloads missing from ACTION_TYPES violates the never constraint and fails to compile
type AssertNever<T extends never> = T;
export type ActionTypesExhaustive = AssertNever<Exclude<keyof ActionPayloads, ActionType>>;
