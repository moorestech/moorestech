import type { BlockInventoryWireData } from "../../types/blockInventoryWireData";
import * as BlockGuids from "../blockLocalizationFixtures";
import { WATER_FLUID_GUID } from "../contentLocalizationFixtures";

const empty = () => ({ itemId: 0, count: 0 });

// BLK-2 電気機械: 入出力+モジュールスロットと machine/electricNetwork capability
// BLK-2 electric machine: in/out+module slots with machine/electricNetwork capabilities
export const blockMachine = {
  open: true,
  source: "block",
  blockType: "ElectricMachine",
  identifier: "block:3",
  blockGuid: BlockGuids.ELECTRIC_MACHINE_BLOCK_GUID,
  // 選択中(bbbbbbbb)の素材itemId2をスロット0に置き、出力/液体スロットは空にしてゴースト描画を検証する
  // Places selected recipe (bbbbbbbb)'s material itemId 2 in slot 0 and leaves the output/fluid slots empty to exercise ghost rendering
  itemSlots: [{ itemId: 2, count: 5 }, empty(), empty(), empty()],
  // 入力タンク1本＋出力タンク1本。液体のみ入出力レシピ(cccccccc)を選ぶと出力液体がtank 1へ束縛される
  // 空タンクのゴースト描画はcapacityを読まないため2本を同容量にし、束縛先の取り違えは量バッジ(spec側)で検証する
  // One input tank plus one output tank; selecting the fluid-only recipe (cccccccc) binds its output fluid to tank 1
  // Both tanks share one capacity because empty-tank ghost rendering ignores it; a swapped binding is caught by the amount badge in the spec instead
  fluidSlots: [{ fluidId: 0, amount: 0, capacity: 100.0, fluidGuid: "" }, { fluidId: 0, amount: 0, capacity: 100.0, fluidGuid: "" }],
  progress: 0.42,
  machine: {
    recipeGuid: "00000000-0000-0000-0000-000000000000",
    selectedRecipeGuid: "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb",
    blockGuid: BlockGuids.ELECTRIC_MACHINE_BLOCK_GUID,
    recipeTime: 15,
    outputItems: [{ itemId: 7, count: 3 }],
    currentState: "processing",
    currentPower: 80.0,
    requestPower: 100.0,
    slotLayout: { input: 2, output: 1, module: 1, inputTank: 1 },
    // 選択中(bbbbbbbb)のレシピ束縛。ホストが配信する正本と同じ形をmockでも持たせる
    // Binding of the selected recipe (bbbbbbbb); the mock carries the same shape the host publishes
    slotBindings: [{ slot: 0, itemId: 2, count: 3, acceptedItemIds: [2] }, { slot: 2, itemId: 7, count: 2, acceptedItemIds: [7] }],
    tankBindings: [{ tank: 0, fluidGuid: WATER_FLUID_GUID, amount: 10 }],
  },
  electricNetwork: { totalGeneratePower: 500.0, totalRequiredPower: 300.0, consumerCount: 4, powerRate: 1.0 },
} satisfies BlockInventoryWireData;

// BLK-3 ギア機械: machine + gear/gearNetwork capability
// BLK-3 gear machine: machine + gear/gearNetwork capabilities
export const blockGearMachine = {
  open: true,
  source: "block",
  blockType: "GearMachine",
  identifier: "block:4",
  blockGuid: BlockGuids.GEAR_MACHINE_BLOCK_GUID,
  itemSlots: [{ itemId: 3, count: 2 }, empty()],
  fluidSlots: [],
  progress: 0.1,
  machine: {
    recipeGuid: "00000000-0000-0000-0000-000000000000",
    selectedRecipeGuid: "00000000-0000-0000-0000-000000000000",
    blockGuid: BlockGuids.GEAR_MACHINE_BLOCK_GUID,
    recipeTime: 15,
    outputItems: [{ itemId: 7, count: 3 }],
    // 充足率62%の不足稼働。歯車駆動なので赤を出さないことをe2eで固定する
    // Running short at 62% satisfaction; gear drive must stay free of red, pinned by e2e
    currentState: "processing",
    currentPower: 37.5,
    requestPower: 60.0,
    slotLayout: { input: 1, output: 1, module: 0, inputTank: 0 },
    slotBindings: [],
    tankBindings: [],
  },
  gear: { currentRpm: 12.5, currentTorque: 3.0, baseRpm: 20.0, role: "consumer" },
  gearNetwork: { totalRequiredGearPower: 60.0, totalGenerateGearPower: 100.0, stopReason: "none" },
} satisfies BlockInventoryWireData;
