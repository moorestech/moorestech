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
  "ui.modal.respond": { id: string; result: "confirm" | "cancel" };
  "block_inventory.move_item": { from: BlockSlotRef; to: BlockSlotRef; count: number };
  "block_inventory.collect": { slot: BlockSlotRef };
  "ui_state.request": { state: "GameScreen" | "PlayerInventory" };
  "research.complete": { researchGuid: string };
  "filter_splitter.set_mode": { directionIndex: number; mode: "default" | "whitelist" | "blacklist" };
  // clear:true は右クリック相当のフィルタ解除。clear:false は C# 側が Grab の持ち手アイテムを設定する
  // clear:true clears the filter (right-click); with clear:false the C# side assigns the currently grabbed item
  "filter_splitter.set_filter_item": { directionIndex: number; slotIndex: number; clear: boolean };
  "debug.echo": { hello: string };
};
