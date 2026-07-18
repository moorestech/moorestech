import type { z } from "zod";
import type {
  BlockInventoryAreaSchema,
  BlockInventoryClosedSchema,
  BlockInventoryDataSchema,
  BlockInventoryOpenSchema,
  BlockSlotRefSchema,
  BuildMenuDataSchema,
  BuildMenuEntryDataSchema,
  BuildMenuEntryTypeSchema,
  CraftRecipeSchema,
  CraftRecipesDataSchema,
  ElectricNetworkDataSchema,
  ElectricToGearDataSchema,
  ElectricToGearOutputModeDataSchema,
  FilterSplitterDataSchema,
  FilterSplitterDirectionDataSchema,
  FilterSplitterModeSchema,
  FluidSlotDataSchema,
  GearDetailDataSchema,
  GearNetworkDataSchema,
  GearNetworkStopReasonSchema,
  GeneratorDetailDataSchema,
  InventoryAreaSchema,
  ItemMasterDataSchema,
  ItemMasterEntrySchema,
  MachineDetailDataSchema,
  MachineRecipeItemSchema,
  MachineRecipeSchema,
  MachineRecipesDataSchema,
  MinerDetailDataSchema,
  ModalDataSchema,
  ModalRequestSchema,
  PlayerInventoryDataSchema,
  ProgressDataSchema,
  RecipeViewerItemListDataSchema,
  RequiredItemSchema,
  ResearchNodeDataSchema,
  ResearchNodeStateSchema,
  ResearchTreeDataSchema,
  SlotDataSchema,
  SlotRefSchema,
  UiStateDataSchema,
  LocalizationDataSchema,
  ChallengeNodeStateSchema,
  ChallengeNodeDataSchema,
  ChallengeCategoryDataSchema,
  ChallengeTreeDataSchema,
  CurrentChallengeDataSchema,
  ChallengeCurrentDataSchema,
  PauseMenuDataSchema,
  PlacementModeDataSchema,
  DeleteModeDataSchema,
  KeyHintsDataSchema,
  CrosshairDataSchema,
  UiVisibilityDataSchema,
  MiningHudDataSchema,
  TooltipDataSchema,
  ContextMenuDataSchema,
  GameStateDataSchema,
  TutorialPresentationDataSchema,
  SkitPresentationDataSchema,
} from "./schemas";

// 公開DTO型は実行時スキーマから導出し、wire shapeの定義元を一つに保つ
// Derive public DTO types from runtime schemas to keep one source for wire shapes
export type SlotData = z.infer<typeof SlotDataSchema>;
export type InventoryArea = z.infer<typeof InventoryAreaSchema>;
export type SlotRef = z.infer<typeof SlotRefSchema>;
export type BlockInventoryArea = z.infer<typeof BlockInventoryAreaSchema>;
export type BlockSlotRef = z.infer<typeof BlockSlotRefSchema>;
export type PlayerInventoryData = z.infer<typeof PlayerInventoryDataSchema>;
export type ModalRequest = z.infer<typeof ModalRequestSchema>;
export type ModalData = z.infer<typeof ModalDataSchema>;
export type ProgressData = z.infer<typeof ProgressDataSchema>;
export type UiStateData = z.infer<typeof UiStateDataSchema>;
export type LocalizationData = z.infer<typeof LocalizationDataSchema>;
export type PauseMenuData = z.infer<typeof PauseMenuDataSchema>;
export type PlacementModeData = z.infer<typeof PlacementModeDataSchema>;
export type DeleteModeData = z.infer<typeof DeleteModeDataSchema>;
export type KeyHintsData = z.infer<typeof KeyHintsDataSchema>;
export type CrosshairData = z.infer<typeof CrosshairDataSchema>;
export type UiVisibilityData = z.infer<typeof UiVisibilityDataSchema>;
export type MiningHudData = z.infer<typeof MiningHudDataSchema>;
export type TooltipData = z.infer<typeof TooltipDataSchema>;
export type ContextMenuData = z.infer<typeof ContextMenuDataSchema>;
export type GameStateData = z.infer<typeof GameStateDataSchema>;
export type TutorialPresentationData = z.infer<typeof TutorialPresentationDataSchema>;
export type SkitPresentationData = z.infer<typeof SkitPresentationDataSchema>;
export type FluidSlotData = z.infer<typeof FluidSlotDataSchema>;
export type MachineDetailData = z.infer<typeof MachineDetailDataSchema>;
export type GeneratorDetailData = z.infer<typeof GeneratorDetailDataSchema>;
export type MinerDetailData = z.infer<typeof MinerDetailDataSchema>;
export type GearDetailData = z.infer<typeof GearDetailDataSchema>;
export type ElectricNetworkData = z.infer<typeof ElectricNetworkDataSchema>;
export type ElectricToGearOutputModeData = z.infer<typeof ElectricToGearOutputModeDataSchema>;
export type ElectricToGearData = z.infer<typeof ElectricToGearDataSchema>;
export type GearNetworkStopReason = z.infer<typeof GearNetworkStopReasonSchema>;
export type GearNetworkData = z.infer<typeof GearNetworkDataSchema>;
export type FilterSplitterMode = z.infer<typeof FilterSplitterModeSchema>;
export type FilterSplitterDirectionData = z.infer<typeof FilterSplitterDirectionDataSchema>;
export type FilterSplitterData = z.infer<typeof FilterSplitterDataSchema>;
export type BlockInventoryOpen = z.infer<typeof BlockInventoryOpenSchema>;
export type BlockInventoryClosed = z.infer<typeof BlockInventoryClosedSchema>;
export type BlockInventoryData = z.infer<typeof BlockInventoryDataSchema>;
export type RequiredItem = z.infer<typeof RequiredItemSchema>;
export type CraftRecipe = z.infer<typeof CraftRecipeSchema>;
export type CraftRecipesData = z.infer<typeof CraftRecipesDataSchema>;
export type MachineRecipeItem = z.infer<typeof MachineRecipeItemSchema>;
export type MachineRecipe = z.infer<typeof MachineRecipeSchema>;
export type MachineRecipesData = z.infer<typeof MachineRecipesDataSchema>;
export type RecipeViewerItemListData = z.infer<typeof RecipeViewerItemListDataSchema>;
export type ItemMasterEntry = z.infer<typeof ItemMasterEntrySchema>;
export type ItemMasterData = z.infer<typeof ItemMasterDataSchema>;
export type ResearchNodeState = z.infer<typeof ResearchNodeStateSchema>;
export type ResearchNodeData = z.infer<typeof ResearchNodeDataSchema>;
export type ResearchTreeData = z.infer<typeof ResearchTreeDataSchema>;
export type BuildMenuEntryType = z.infer<typeof BuildMenuEntryTypeSchema>;
export type BuildMenuEntryData = z.infer<typeof BuildMenuEntryDataSchema>;
export type BuildMenuData = z.infer<typeof BuildMenuDataSchema>;
export type ChallengeNodeState = z.infer<typeof ChallengeNodeStateSchema>;
export type ChallengeNodeData = z.infer<typeof ChallengeNodeDataSchema>;
export type ChallengeCategoryData = z.infer<typeof ChallengeCategoryDataSchema>;
export type ChallengeTreeData = z.infer<typeof ChallengeTreeDataSchema>;
export type CurrentChallengeData = z.infer<typeof CurrentChallengeDataSchema>;
export type ChallengeCurrentData = z.infer<typeof ChallengeCurrentDataSchema>;
