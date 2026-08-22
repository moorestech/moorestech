import type { z } from "zod";
import { Topics, type TopicPayloads } from "../transport/protocol";
import {
  BlockInventoryDataSchema,
  BuildMenuDataSchema,
  CraftRecipesDataSchema,
  HotbarDataSchema,
  MachineRecipesDataSchema,
  ModalDataSchema,
  PlayerInventoryDataSchema,
  ProgressDataSchema,
  RecipeViewerItemListDataSchema,
  ResearchTreeDataSchema,
  UiStateDataSchema,
  LocalizationDataSchema,
  ChallengeTreeDataSchema,
  ChallengeCurrentDataSchema,
  PauseMenuDataSchema,
  PlacementModeDataSchema,
  CrosshairDataSchema,
  UiVisibilityDataSchema,
  TooltipDataSchema,
  GameStateDataSchema,
  TutorialPresentationDataSchema,
  WorldPinPresentationDataSchema,
  SkitPresentationDataSchema,
  TrainRidingDataSchema,
  NotificationDataSchema,
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
  [Topics.hotbar]: HotbarDataSchema,
  [Topics.localization]: LocalizationDataSchema,
  [Topics.challengeTree]: ChallengeTreeDataSchema,
  [Topics.challengeCurrent]: ChallengeCurrentDataSchema,
  [Topics.pauseMenu]: PauseMenuDataSchema,
  [Topics.placementMode]: PlacementModeDataSchema,
  [Topics.crosshair]: CrosshairDataSchema,
  [Topics.uiVisibility]: UiVisibilityDataSchema,
  [Topics.tooltip]: TooltipDataSchema,
  [Topics.gameState]: GameStateDataSchema,
  [Topics.tutorialPresentation]: TutorialPresentationDataSchema,
  [Topics.worldPins]: WorldPinPresentationDataSchema,
  [Topics.skitPresentation]: SkitPresentationDataSchema,
  [Topics.trainRiding]: TrainRidingDataSchema,
  [Topics.notification]: NotificationDataSchema,
} satisfies TopicSchemaRegistry;

// 既知topicは検証と同時に変換後の値を返す。判別union化したスキーマは変換結果こそが正
// Known topics return the transformed value alongside validation; for discriminated-union schemas the transformed value is the real one
// 未知topicは従来どおり素通しする
// Unknown topics still pass through unchanged
export function parseTopicPayload(topic: string, data: unknown): { valid: true; value: unknown } | { valid: false } {
  const schema = topicSchemas[topic as keyof typeof topicSchemas];
  if (schema === undefined) return { valid: true, value: data };

  const result = schema.safeParse(data);
  return result.success ? { valid: true, value: result.data } : { valid: false };
}
