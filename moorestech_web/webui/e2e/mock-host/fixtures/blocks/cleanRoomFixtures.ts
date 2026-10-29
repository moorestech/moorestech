import type { BlockInventoryWireData } from "../../types/blockInventoryWireData";
import { WATER_FLUID_GUID } from "../contentLocalizationFixtures";
import { blockMachine } from "./machineFixtures";

const airFilterAt = (x: number) => ({ x, y: 33, z: 5, powerRatio: 1, filterItemCount: 5, filterRemainingRatio: 0.5 });

// 部屋欄付きの大型機械パネル。入力タンクにIPA相当の液体
// Interior machine: the large recipe-selected panel topped by the room section, with fluid (the real IPA stand-in) in the input tank
function cleanRoomMachineWith(airFilterCount: number, identifier: string) {
  return {
    ...blockMachine,
    blockType: "CleanRoomMachine",
    identifier,
    fluidSlots: [{ fluidId: 1, amount: 50, capacity: 100.0, fluidGuid: WATER_FLUID_GUID }, { fluidId: 0, amount: 0, capacity: 100.0, fluidGuid: "" }],
    cleanRoom: {
      fetchState: "fetched",
      kind: "enclosed",
      room: {
        headline: "class",
        className: "ISO 5",
        concentration: 1.2,
        airChangeRate: 0.8,
        volume: 74,
        airFilters: Array.from({ length: airFilterCount }, (_, index) => airFilterAt(index + 1)),
      },
    },
  } satisfies BlockInventoryWireData;
}

// 清浄機2台: 実機で液体行が電力行に重なった再現形
// Two purifiers: the shape that overlapped the fluid row onto the power row in the real game
export const blockCleanRoomMachine = cleanRoomMachineWith(2, "block:40");

// 清浄機が多く部屋欄が伸びる部屋
// A room with many purifiers: checks the footer is not pushed out as the room section grows
export const blockCleanRoomMachineManyFilters = cleanRoomMachineWith(8, "block:41");
