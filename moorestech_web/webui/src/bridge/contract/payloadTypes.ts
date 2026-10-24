import type { z } from "zod";
import type {
  BlockInventoryDataSchema,
  BlockInventoryOpenSchema,
  BlockSlotRefSchema,
  BuildMenuCategorySchema,
  BuildMenuDataSchema,
  BuildMenuEntryDataSchema,
  BuildMenuRequiredItemSchema,
  CraftRecipeSchema,
  CraftRecipesDataSchema,
  GearToElectricGenerationStateSchema,
  FilterSplitterModeSchema,
  FluidMasterDataSchema,
  FluidMasterEntrySchema,
  FluidSlotDataSchema,
  GearDetailDataSchema,
  BeltThroughputDataSchema,
  BeltThroughputWindowDataSchema,
  GearNetworkStopReasonSchema,
  HotbarDataSchema,
  HotbarSlotSchema,
  ItemMasterDataSchema,
  ItemMasterEntrySchema,
  MachineDetailDataSchema,
  MachineProcessStateSchema,
  MachineRecipeSchema,
  MachineRecipesDataSchema,
  MachineRecipeLevelInheritanceSchema,
  MachineUpgradeDataSchema,
  ModalDataSchema,
  ModalRequestSchema,
  PlayerInventoryDataSchema,
  ProgressDataSchema,
  RecipeViewerItemListDataSchema,
  ResearchNodeDataSchema,
  ResearchNodeStateSchema,
  ResearchTreeDataSchema,
  SlotDataSchema,
  SlotRefSchema,
  UiStateDataSchema,
  LocalizationDataSchema,
  SprintInputModeDataSchema,
  GraphicsQualityDataSchema,
  GraphicsQualitySettingsSchema,
  EventLanguageGateDataSchema,
  ChallengeNodeDataSchema,
  ChallengeTreeDataSchema,
  ChallengeCurrentDataSchema,
  PauseMenuDataSchema,
  PlacementModeDataSchema,
  CrosshairDataSchema,
  UiVisibilityDataSchema,
  TooltipDataSchema,
  TooltipLineSchema,
  GameStateDataSchema,
  TutorialPresentationDataSchema,
  WorldPinPresentationDataSchema,
  SkitPresentationDataSchema,
  TrainRidingDataSchema,
  TrainPlatformModeSchema,
  TrainTimetableDataSchema,
  TrainTimetableStationSchema,
  TrainTimetableStopSchema,
  CleanRoomDetailDataSchema,
  CleanRoomSummaryDataSchema,
  NotificationDataSchema,
} from "./schemas";

// 公開DTO型は実行時スキーマから導出し、wire shapeの定義元を一つに保つ
// Derive public DTO types from runtime schemas to keep one source for wire shapes
export type SlotData = z.infer<typeof SlotDataSchema>;
export type SlotRef = z.infer<typeof SlotRefSchema>;
export type BlockSlotRef = z.infer<typeof BlockSlotRefSchema>;
export type PlayerInventoryData = z.infer<typeof PlayerInventoryDataSchema>;
export type ModalRequest = z.infer<typeof ModalRequestSchema>;
export type ModalData = z.infer<typeof ModalDataSchema>;
export type ProgressData = z.infer<typeof ProgressDataSchema>;
export type UiStateData = z.infer<typeof UiStateDataSchema>;
export type SprintInputModeData = z.infer<typeof SprintInputModeDataSchema>;
export type SprintInputMode = SprintInputModeData["mode"];
export type GraphicsQualityData = z.infer<typeof GraphicsQualityDataSchema>;
export type GraphicsQualitySettings = z.infer<typeof GraphicsQualitySettingsSchema>;
// 項目と対応する値だけを送り、未変更値の上書きを防ぐ
// Send only the selected item and its matching value to prevent overwriting untouched values
export type GraphicsQualityChange = {
  [K in keyof GraphicsQualitySettings]: {
    item: K;
    value: GraphicsQualitySettings[K];
  };
}[keyof GraphicsQualitySettings];
export type GraphicsQualityPresetChoice = Exclude<GraphicsQualityData["preset"], "custom">;
export type LocalizationData = z.infer<typeof LocalizationDataSchema>;
export type EventLanguageGateData = z.infer<typeof EventLanguageGateDataSchema>;
export type PauseMenuData = z.infer<typeof PauseMenuDataSchema>;
export type PlacementModeData = z.infer<typeof PlacementModeDataSchema>;
export type CrosshairData = z.infer<typeof CrosshairDataSchema>;
export type UiVisibilityData = z.infer<typeof UiVisibilityDataSchema>;
export type TooltipData = z.infer<typeof TooltipDataSchema>;
// 表示中の枝だけを受ける型。行が空の枝を渡せないことを型で保証する
// The visible branch alone; the type makes an empty-line payload unpassable
export type VisibleTooltipData = Extract<TooltipData, { visible: true }>;
export type TooltipLine = z.infer<typeof TooltipLineSchema>;
export type GameStateData = z.infer<typeof GameStateDataSchema>;
export type TutorialPresentationData = z.infer<typeof TutorialPresentationDataSchema>;
export type WorldPinPresentationData = z.infer<typeof WorldPinPresentationDataSchema>;
export type SkitPresentationData = z.infer<typeof SkitPresentationDataSchema>;
// intent語彙は契約のenumが唯一の定義。利用側のSet<string>化で型検査を失わないための別名
// The contract enum is the sole intent vocabulary; this alias keeps consumers from widening to Set<string>
export type SkitIntent = SkitPresentationData["allowedIntents"][number];
export type TrainRidingData = z.infer<typeof TrainRidingDataSchema>;
export type FluidSlotData = z.infer<typeof FluidSlotDataSchema>;
export type MachineProcessState = z.infer<typeof MachineProcessStateSchema>;
export type MachineDetailData = z.infer<typeof MachineDetailDataSchema>;
export type GearDetailData = z.infer<typeof GearDetailDataSchema>;
export type BeltThroughputData = z.infer<typeof BeltThroughputDataSchema>;
export type BeltThroughputWindowData = z.infer<typeof BeltThroughputWindowDataSchema>;
export type CleanRoomDetailData = z.infer<typeof CleanRoomDetailDataSchema>;
export type CleanRoomSummaryData = z.infer<typeof CleanRoomSummaryDataSchema>;
export type GearToElectricGenerationState = z.infer<typeof GearToElectricGenerationStateSchema>;
export type TrainPlatformMode = z.infer<typeof TrainPlatformModeSchema>;
export type TrainTimetableData = z.infer<typeof TrainTimetableDataSchema>;
export type TrainTimetableStation = z.infer<typeof TrainTimetableStationSchema>;
export type TrainTimetableStop = z.infer<typeof TrainTimetableStopSchema>;
export type GearNetworkStopReason = z.infer<typeof GearNetworkStopReasonSchema>;
export type FilterSplitterMode = z.infer<typeof FilterSplitterModeSchema>;
export type BlockInventoryOpen = z.infer<typeof BlockInventoryOpenSchema>;
export type BlockInventoryData = z.infer<typeof BlockInventoryDataSchema>;
export type CraftRecipe = z.infer<typeof CraftRecipeSchema>;
export type CraftRecipesData = z.infer<typeof CraftRecipesDataSchema>;
export type MachineRecipe = z.infer<typeof MachineRecipeSchema>;
export type MachineRecipesData = z.infer<typeof MachineRecipesDataSchema>;
export type MachineRecipeLevelInheritance = z.infer<typeof MachineRecipeLevelInheritanceSchema>;
export type MachineUpgradeData = z.infer<typeof MachineUpgradeDataSchema>;
export type RecipeViewerItemListData = z.infer<typeof RecipeViewerItemListDataSchema>;
export type ItemMasterEntry = z.infer<typeof ItemMasterEntrySchema>;
export type ItemMasterData = z.infer<typeof ItemMasterDataSchema>;
export type FluidMasterEntry = z.infer<typeof FluidMasterEntrySchema>;
export type FluidMasterData = z.infer<typeof FluidMasterDataSchema>;
export type ResearchNodeState = z.infer<typeof ResearchNodeStateSchema>;
export type ResearchNodeData = z.infer<typeof ResearchNodeDataSchema>;
export type ResearchTreeData = z.infer<typeof ResearchTreeDataSchema>;
export type BuildMenuRequiredItem = z.infer<typeof BuildMenuRequiredItemSchema>;
export type BuildMenuCategory = z.infer<typeof BuildMenuCategorySchema>;
export type BuildMenuEntryData = z.infer<typeof BuildMenuEntryDataSchema>;
export type BuildMenuData = z.infer<typeof BuildMenuDataSchema>;
export type HotbarSlot = z.infer<typeof HotbarSlotSchema>;
export type HotbarData = z.infer<typeof HotbarDataSchema>;
export type ChallengeNodeData = z.infer<typeof ChallengeNodeDataSchema>;
export type ChallengeTreeData = z.infer<typeof ChallengeTreeDataSchema>;
export type ChallengeCurrentData = z.infer<typeof ChallengeCurrentDataSchema>;
export type NotificationData = z.infer<typeof NotificationDataSchema>;
