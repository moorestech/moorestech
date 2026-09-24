import type { BlockInventoryWireData } from "../../../src/bridge/contract/payloadTypes";

const empty = () => ({ itemId: 0, count: 0 });

// 駅名有無と現在停車駅を持つ列車を再現
// Reproduces a train with named/unnamed stations and a current stop
export const trainWithTimetable = {
  open: true,
  source: "train",
  blockType: "Train",
  identifier: "train:103",
  itemSlots: Array.from({ length: 10 }, empty),
  fluidSlots: [],
  timetable: {
    trainUnitId: "unit-1",
    isAutoRun: false,
    currentIndex: 0,
    stops: [{ position: { x: 12, y: 0, z: 40 }, name: "北駅" }],
    stations: [
      { position: { x: -8, y: 0, z: 3 }, name: "" },
      { position: { x: 12, y: 0, z: 40 }, name: "北駅" },
    ],
  },
} satisfies BlockInventoryWireData;
