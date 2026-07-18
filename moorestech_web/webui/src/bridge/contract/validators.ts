import type { z } from "zod";
import { Topics, type TopicPayloads } from "../transport/protocol";
import {
  BlockInventoryDataSchema,
  BuildMenuDataSchema,
  CraftRecipesDataSchema,
  MachineRecipesDataSchema,
  ModalDataSchema,
  PlayerInventoryDataSchema,
  ProgressDataSchema,
  RecipeViewerItemListDataSchema,
  ResearchTreeDataSchema,
  UiStateDataSchema,
  LocalizationDataSchema,
  PauseMenuDataSchema,
  PlacementModeDataSchema,
  DeleteModeDataSchema,
  KeyHintsDataSchema,
  CrosshairDataSchema,
  UiVisibilityDataSchema,
  MiningHudDataSchema,
  TooltipDataSchema,
  ContextMenuDataSchema,
} from "./schemas";

type TopicSchemaRegistry = {
  [K in keyof TopicPayloads]: z.ZodType<TopicPayloads[K]>;
};

// topic追加時に対応スキーマが無ければ型検査を失敗させる
// Fail type checking when a topic is added without a matching schema
const topicSchemas = {
  [Topics.inventory]: PlayerInventoryDataSchema,
  [Topics.craftRecipes]: CraftRecipesDataSchema,
  [Topics.machineRecipes]: MachineRecipesDataSchema,
  [Topics.itemList]: RecipeViewerItemListDataSchema,
  [Topics.blockInventory]: BlockInventoryDataSchema,
  [Topics.modal]: ModalDataSchema,
  [Topics.progress]: ProgressDataSchema,
  [Topics.uiState]: UiStateDataSchema,
  [Topics.researchTree]: ResearchTreeDataSchema,
  [Topics.buildMenu]: BuildMenuDataSchema,
  [Topics.localization]: LocalizationDataSchema,
  [Topics.pauseMenu]: PauseMenuDataSchema,
  [Topics.placementMode]: PlacementModeDataSchema,
  [Topics.deleteMode]: DeleteModeDataSchema,
  [Topics.keyHints]: KeyHintsDataSchema,
  [Topics.crosshair]: CrosshairDataSchema,
  [Topics.uiVisibility]: UiVisibilityDataSchema,
  [Topics.miningHud]: MiningHudDataSchema,
  [Topics.tooltip]: TooltipDataSchema,
  [Topics.contextMenu]: ContextMenuDataSchema,
} satisfies TopicSchemaRegistry;

// 既知topicはsafeParseで検証し、未知topicは従来どおり素通しする
// Validate known topics with safeParse and preserve pass-through for unknown topics
export function validateTopicPayload(topic: string, data: unknown): boolean {
  const schema = topicSchemas[topic as keyof typeof topicSchemas];
  return schema === undefined || schema.safeParse(data).success;
}
