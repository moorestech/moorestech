import type {
  PlayerInventoryData, CraftRecipesData, MachineRecipesData,
  RecipeViewerItemListData,
  ModalData, ProgressData, BlockInventoryData,
  UiStateData, ResearchTreeData, BuildMenuData,
  LocalizationData, ChallengeTreeData,
  ChallengeCurrentData, PauseMenuData, PlacementModeData,
  DeleteModeData, CrosshairData,
  UiVisibilityData, MiningHudData, TooltipData,
  GameStateData, TutorialPresentationData,
  WorldPinPresentationData,
  SkitPresentationData, TrainRidingData,
} from "../contract/payloadTypes";
import { z } from "zod";

export const TopicEnvelopeSchema = z.object({
  op: z.enum(["snapshot", "event"]),
  topic: z.string(),
  revision: z.number().int().nonnegative(),
  data: z.unknown(),
});

export type TopicEnvelope = z.infer<typeof TopicEnvelopeSchema>;

// 通信の op レベルのメッセージ型（webSocketClient が使用）
// Wire-level message types at the op layer (used by webSocketClient)
export type ServerMsg =
  | TopicEnvelope
  | { op: "pong" }
  | { op: "result"; requestId: string; ok: boolean; error?: string };

export type ClientMsg =
  | { op: "subscribe"; topics: string[] }
  | { op: "unsubscribe"; topics: string[] }
  | { op: "action"; type: string; requestId: string; payload: unknown }
  | { op: "input_state"; pointerOverUi: boolean; textInputFocused: boolean }
  | { op: "ping" }
  | { op: "pong" };

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
  localization: "localization.current",
  challengeTree: "challenge.tree",
  challengeCurrent: "challenge.current",
  pauseMenu: "pause_menu.current",
  placementMode: "ui.placement_mode",
  deleteMode: "ui.delete_mode",
  crosshair: "ui.crosshair",
  uiVisibility: "ui.visibility",
  miningHud: "ui.mining_hud",
  tooltip: "ui.tooltip",
  gameState: "game_state.current",
  tutorialPresentation: "tutorial.presentation",
  worldPins: "tutorial.world_pins",
  skitPresentation: "skit.presentation",
  trainRiding: "train.riding",
  // プレイテスト要求は snapshot を持たない一時イベントとして扱う
  // Playtest requests are transient events without snapshots
  playtestDomQuery: "playtest.dom_query",
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
  [Topics.localization]: LocalizationData;
  [Topics.challengeTree]: ChallengeTreeData;
  [Topics.challengeCurrent]: ChallengeCurrentData;
  [Topics.pauseMenu]: PauseMenuData;
  [Topics.placementMode]: PlacementModeData;
  [Topics.deleteMode]: DeleteModeData;
  [Topics.crosshair]: CrosshairData;
  [Topics.uiVisibility]: UiVisibilityData;
  [Topics.miningHud]: MiningHudData;
  [Topics.tooltip]: TooltipData;
  [Topics.gameState]: GameStateData;
  [Topics.tutorialPresentation]: TutorialPresentationData;
  [Topics.worldPins]: WorldPinPresentationData;
  [Topics.skitPresentation]: SkitPresentationData;
  [Topics.trainRiding]: TrainRidingData;
};

// 200行制限でactionContract.tsへ分離
// Split into actionContract.ts for the 200-line rule
export { UiStateNames, ACTION_TYPES } from "./actionContract";
export type { ActionPayloads, ActionType, ActionTypesExhaustive } from "./actionContract";
