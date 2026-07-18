import type {
  PlayerInventoryData,
  CraftRecipesData,
  MachineRecipesData,
  RecipeViewerItemListData,
  SlotRef,
  BlockSlotRef,
  ModalData,
  ProgressData,
  BlockInventoryData,
  UiStateData,
  ResearchTreeData,
  BuildMenuData,
  BuildMenuEntryType,
} from "../contract/payloadTypes";

// 通信の op レベルのメッセージ型（webSocketClient が使用）
// Wire-level message types at the op layer (used by webSocketClient)
export type ServerMsg =
  | { op: "snapshot"; topic: string; data: unknown }
  | { op: "event"; topic: string; data: unknown }
  | { op: "result"; requestId: string; ok: boolean; error?: string };

export type ClientMsg =
  | { op: "subscribe"; topics: string[] }
  | { op: "unsubscribe"; topics: string[] }
  | { op: "snapshot"; topic: string }
  | { op: "action"; type: string; requestId: string; payload: unknown };

export type ActionResult = { ok: boolean; error?: string };

// topic 名の単一の真実。文字列リテラルの散在を防ぐ
// Single source of truth for topic names; prevents scattered string literals
export const Topics = {
  inventory: "local_player.inventory",
  craftRecipes: "crafting.recipes",
  machineRecipes: "crafting.machine_recipes",
  itemList: "recipe_viewer.item_list",
  blockInventory: "block_inventory.current",
  modal: "ui.modal",
  progress: "ui.progress",
  uiState: "ui_state.current",
  researchTree: "research.tree",
  buildMenu: "build_menu.entries",
} as const;

// C# UIStateEnum 由来の state 名。文字列リテラルの散在を防ぐ
// State names from the C# UIStateEnum; prevents scattered string literals
export const UiStateNames = {
  gameScreen: "GameScreen",
  playerInventory: "PlayerInventory",
  subInventory: "SubInventory",
  researchTree: "ResearchTree",
  buildMenu: "BuildMenu",
} as const;

// topic → payload 型の対応表。useTopic/useTopicSelector がこれで型付けされる
// topic → payload type registry; types useTopic/useTopicSelector
export type TopicPayloads = {
  [Topics.inventory]: PlayerInventoryData;
  [Topics.craftRecipes]: CraftRecipesData;
  [Topics.machineRecipes]: MachineRecipesData;
  [Topics.itemList]: RecipeViewerItemListData;
  [Topics.blockInventory]: BlockInventoryData;
  [Topics.modal]: ModalData;
  [Topics.progress]: ProgressData;
  [Topics.uiState]: UiStateData;
  [Topics.researchTree]: ResearchTreeData;
  [Topics.buildMenu]: BuildMenuData;
};

// action type → payload 型の対応表。dispatchAction がこれで型付けされる
// action type → payload type registry; types dispatchAction
export type ActionPayloads = {
  "inventory.move_item": { from: SlotRef; to: SlotRef; count: number };
  "inventory.split": { from: SlotRef };
  "inventory.collect": { slot: SlotRef };
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
  "research.complete": { researchGuid: string };
  "filter_splitter.set_mode": { directionIndex: number; mode: "default" | "whitelist" | "blacklist" };
  // clear:true は右クリック相当のフィルタ解除。clear:false は C# 側が Grab の持ち手アイテムを設定する
  // clear:true clears the filter (right-click); with clear:false the C# side assigns the currently grabbed item
  "filter_splitter.set_filter_item": { directionIndex: number; slotIndex: number; clear: boolean };
  "debug.echo": { hello: string };
};

// 既知 action type の実行時リスト。ActionPayloads のキーと1:1（下の網羅チェックで担保）
// Runtime list of known action types, 1:1 with ActionPayloads keys (enforced by the check below)
export const ACTION_TYPES = [
  "inventory.move_item",
  "inventory.split",
  "inventory.collect",
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
  "research.complete",
  "filter_splitter.set_mode",
  "filter_splitter.set_filter_item",
  "debug.echo",
] as const satisfies readonly (keyof ActionPayloads)[];

export type ActionType = (typeof ACTION_TYPES)[number];

// ActionPayloads にあって ACTION_TYPES に無いキーがあると never 制約違反でコンパイルエラーになる
// A key in ActionPayloads missing from ACTION_TYPES violates the never constraint and fails to compile
type AssertNever<T extends never> = T;
export type ActionTypesExhaustive = AssertNever<Exclude<keyof ActionPayloads, ActionType>>;
