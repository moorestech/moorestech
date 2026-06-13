import type {
  PlayerInventoryData,
  CraftRecipesData,
  MachineRecipesData,
  RecipeViewerItemListData,
  SlotRef,
} from "./payloadTypes";

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
} as const;

// topic → payload 型の対応表。useTopic/subscribeTopic がこれで型付けされる
// topic → payload type registry; types useTopic/subscribeTopic
export type TopicPayloads = {
  [Topics.inventory]: PlayerInventoryData;
  [Topics.craftRecipes]: CraftRecipesData;
  [Topics.machineRecipes]: MachineRecipesData;
  [Topics.itemList]: RecipeViewerItemListData;
};

// action type → payload 型の対応表。dispatchAction がこれで型付けされる
// action type → payload type registry; types dispatchAction
export type ActionPayloads = {
  "inventory.move_item": { from: SlotRef; to: SlotRef; count: number };
  "inventory.split": { from: SlotRef };
  "inventory.collect": { slot: SlotRef };
  "inventory.sort": Record<string, never>;
  "craft.execute": { recipeGuid: string };
  "debug.echo": { hello: string };
};
