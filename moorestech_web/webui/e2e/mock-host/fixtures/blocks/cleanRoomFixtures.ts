import type { BlockInventoryWireData } from "../../types/blockInventoryWireData";
import { WATER_FLUID_GUID } from "../contentLocalizationFixtures";
import { blockMachine } from "./machineFixtures";

const airFilterAt = (x: number) => ({ x, y: 33, z: 5, powerRatio: 1, filterItemCount: 5, filterRemainingRatio: 0.5 });
const UNSELECTED_RECIPE_GUID = "00000000-0000-0000-0000-000000000000";

// 部屋欄付きの大型機械パネル。入力タンクにIPA相当の液体
// Interior machine: the large recipe-selected panel topped by the room section, with fluid (the real IPA stand-in) in the input tank
function cleanRoomMachineWith(airFilterCount: number, identifier: string, selectedRecipeGuid: string) {
  return {
    ...blockMachine,
    blockType: "CleanRoomMachine",
    identifier,
    machine: { ...blockMachine.machine, selectedRecipeGuid },
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

const selected = blockMachine.machine.selectedRecipeGuid;

// 清浄機自身の小型面。実ホストと同形（フィルタ1枠・液体なし・部屋の全台）で、電力網は接続時だけ載る
// The purifier's own small panel, shaped like the real host (one filter slot, no fluids, every unit in the room); the grid rides only when connected
function cleanRoomAirFilterWith(airFilterCount: number, identifier: string, connected: boolean) {
  return {
    open: true,
    source: "block",
    blockType: "CleanRoomAirFilter",
    identifier,
    blockGuid: blockMachine.blockGuid,
    itemSlots: [{ itemId: 1, count: 5 }],
    fluidSlots: [],
    ...(connected ? { electricNetwork: blockMachine.electricNetwork } : {}),
    cleanRoom: cleanRoomMachineWith(airFilterCount, identifier, selected).cleanRoom,
  } satisfies BlockInventoryWireData;
}

// 台数上限が無いので境目と多台数を持つ
// Purifier counts are uncapped per room, so keep both the boundary and many-unit shapes; a max-volume class-A room needs 14+
export const cleanRoomBlockFixtures: Record<string, BlockInventoryWireData> = {
  cleanRoomMachine: cleanRoomMachineWith(2, "block:40", selected),
  cleanRoomMachineManyFilters: cleanRoomMachineWith(8, "block:41", selected),
  cleanRoomMachineNoFilters: cleanRoomMachineWith(0, "block:42", selected),
  cleanRoomMachine16Filters: cleanRoomMachineWith(16, "block:43", selected),
  cleanRoomMachine20Filters: cleanRoomMachineWith(20, "block:44", selected),
  cleanRoomMachine20FiltersSelecting: cleanRoomMachineWith(20, "block:45", UNSELECTED_RECIPE_GUID),
  cleanRoomAirFilter2Filters: cleanRoomAirFilterWith(2, "block:46", true),
  cleanRoomAirFilter8Filters: cleanRoomAirFilterWith(8, "block:47", true),
  cleanRoomAirFilter14Filters: cleanRoomAirFilterWith(14, "block:48", true),
  cleanRoomAirFilter20Filters: cleanRoomAirFilterWith(20, "block:49", true),
  cleanRoomAirFilter30Filters: cleanRoomAirFilterWith(30, "block:50", true),
  cleanRoomAirFilter30FiltersUnconnected: cleanRoomAirFilterWith(30, "block:51", false),
};
