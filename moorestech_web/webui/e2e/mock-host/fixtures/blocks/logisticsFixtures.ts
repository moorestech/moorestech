import type { BlockInventoryWireData } from "../../types/blockInventoryWireData";
import * as BlockGuids from "../blockLocalizationFixtures";
import { WATER_FLUID_GUID } from "../contentLocalizationFixtures";

const empty = () => ({ itemId: 0, count: 0 });

// BLK-7 未登録種別: generic fallback の item/fluid 表示を検証する
// BLK-7 unregistered type: exercises the generic item/fluid fallback
export const blockGeneric = {
  open: true,
  source: "block",
  blockType: "UnregisteredBlock",
  identifier: "block:9",
  blockGuid: BlockGuids.GENERIC_BLOCK_GUID,
  itemSlots: [{ itemId: 1, count: 1 }],
  fluidSlots: [{ fluidId: 1, amount: 10, capacity: 20, fluidGuid: WATER_FLUID_GUID }],
  progress: 0.5,
} satisfies BlockInventoryWireData;

// BLK-8 フィルタ分岐器: 3方向×2フィルタスロットの filterSplitter capability
// BLK-8 filter splitter: filterSplitter capability with 3 directions x 2 filter slots
export const blockFilterSplitter = {
  open: true,
  source: "block",
  blockType: "FilterSplitter",
  identifier: "block:7",
  blockGuid: BlockGuids.FILTER_SPLITTER_BLOCK_GUID,
  itemSlots: [],
  fluidSlots: [],
  filterSplitter: {
    directionCount: 3,
    filterSlotCountPerDirection: 2,
    directions: [
      { mode: "whitelist", filterItemIds: [4, 0] },
      { mode: "default", filterItemIds: [0, 0] },
      { mode: "blacklist", filterItemIds: [7, 8] },
    ],
  },
} satisfies BlockInventoryWireData;

// B2 列車PFのスロットとモード
// B2 train platform: item slots and transfer mode
export const blockTrainPlatform = {
  open: true,
  source: "block",
  blockType: "TrainItemPlatform",
  identifier: "block:11",
  blockGuid: BlockGuids.TRAIN_ITEM_PLATFORM_BLOCK_GUID,
  itemSlots: [{ itemId: 3, count: 12 }, empty()],
  fluidSlots: [],
  trainPlatform: { mode: "loadToTrain", itemSlotCount: 2 },
} satisfies BlockInventoryWireData;

// B2 液体PFのマスタ容量
// B2 fluid platform: the same master capacity shown by uGUI
export const blockTrainFluidPlatform = {
  open: true,
  source: "block",
  blockType: "TrainFluidPlatform",
  identifier: "block:12",
  blockGuid: BlockGuids.TRAIN_FLUID_PLATFORM_BLOCK_GUID,
  itemSlots: [],
  fluidSlots: [],
  trainPlatform: { mode: "unloadToPlatform", fluidCapacity: 1000 },
} satisfies BlockInventoryWireData;
