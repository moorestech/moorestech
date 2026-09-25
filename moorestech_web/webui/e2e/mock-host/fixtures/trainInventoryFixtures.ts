import type { BlockInventoryWireData } from "../../../src/bridge/contract/payloadTypes";

const empty = () => ({ itemId: 0, count: 0 });

export const trainCargo = {
  open: true,
  source: "train",
  blockType: "Train",
  identifier: "train:101",
  itemSlots: [{ itemId: 1, count: 24 }, { itemId: 2, count: 8 }, ...Array.from({ length: 7 }, empty)],
  fluidSlots: [],
  // 時刻表タブを開く前のC#は未取得＝読み込み中を送る
  // Before the timetable tab opens, C# sends the not-yet-fetched loading state
  timetable: { kind: "loading" },
} satisfies BlockInventoryWireData;

export const trainContainerMissing = {
  open: true,
  source: "train",
  blockType: "Train",
  identifier: "train:102",
  itemSlots: [],
  fluidSlots: [],
  // 車両が解決できないときC#は取得不可を送る
  // C# sends unavailable when the car cannot be resolved
  timetable: { kind: "unavailable" },
  error: "containerMissing",
} satisfies BlockInventoryWireData;

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
    kind: "ready",
    data: {
      trainUnitId: "unit-1",
      isAutoRun: false,
      currentIndex: 0,
      stops: [{ position: { x: 12, y: 0, z: 40 }, name: "北駅", side: "back" }],
      stations: [
        { position: { x: -8, y: 0, z: 3 }, name: "" },
        { position: { x: 12, y: 0, z: 40 }, name: "北駅" },
      ],
    },
  },
} satisfies BlockInventoryWireData;
