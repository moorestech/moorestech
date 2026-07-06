import { describe, it, expect, vi } from "vitest";

// 解決対象コンポーネントは BlockItemGrid 経由で webSocketClient を読み込む。
// import 時に location.host を触るため node 環境では stub する
// The resolved components import webSocketClient via BlockItemGrid, which touches location.host at import;
// stub it so this node-env test can load the component tree
vi.mock("@/bridge/transport/webSocketClient", () => ({ sendAction: vi.fn() }));

import {
  blockShiftMovePayloads,
  blockSlotClickPayload,
  blockSlotRightClickPayload,
  pickUpPayload,
  placePayload,
  resolveBlockComponent,
} from "./blockLogic";
import ChestInventory from "./views/ChestInventory";
import TankInventory from "./views/TankInventory";
import GenericBlockInventory from "./views/GenericBlockInventory";

describe("pickUpPayload", () => {
  it("block スロット→grab へ count ごと拾う payload を作る", () => {
    expect(pickUpPayload(3, 7)).toEqual({
      from: { area: "block", slot: 3 },
      to: { area: "grab", slot: 0 },
      count: 7,
    });
  });
});

describe("placePayload", () => {
  it("grab→block スロットへ grabCount ごと置く payload を作る", () => {
    expect(placePayload(2, 5)).toEqual({
      from: { area: "grab", slot: 0 },
      to: { area: "block", slot: 2 },
      count: 5,
    });
  });
});

describe("blockSlotClickPayload", () => {
  it("grab 保持時は grabCount ごと place payload を返す（スロットが空でも置く）", () => {
    expect(blockSlotClickPayload(1, 0, 0, 4)).toEqual(placePayload(1, 4));
  });
  it("grab 無し + 中身ありは slot.count 全量の pickup payload を返す", () => {
    expect(blockSlotClickPayload(2, 10, 6, 0)).toEqual(pickUpPayload(2, 6));
  });
  it("grab 無し + スロット空は null を返す（無操作）", () => {
    expect(blockSlotClickPayload(3, 0, 0, 0)).toBeNull();
  });
});

describe("resolveBlockComponent", () => {
  it("Chest(実マスタ値) は ChestInventory を返す", () => {
    expect(resolveBlockComponent("Chest")).toBe(ChestInventory);
  });
  it("小文字 chest は実マスタ値でないため fallback になる", () => {
    expect(resolveBlockComponent("chest")).toBe(GenericBlockInventory);
  });
  it("tank は登録キー削除済みのためフォールバックを返す", () => {
    expect(resolveBlockComponent("tank")).toBe(GenericBlockInventory);
  });
  it("未登録 blockType はフォールバックを返す", () => {
    expect(resolveBlockComponent("unknown")).toBe(GenericBlockInventory);
  });
  it.each([
    "ElectricMachine",
    "GearMachine",
    "ElectricGenerator",
    "FuelGearGenerator",
    "SimpleGearGenerator",
    "ElectricMiner",
    "GearMiner",
  ])("resolves a dedicated view for %s", (blockType) => {
    expect(resolveBlockComponent(blockType)).not.toBe(GenericBlockInventory);
  });
});

// TankInventory は専用 UI として温存（実流体ブロック配線時に再登録する想定）
// TankInventory is kept as a dedicated UI (to be re-registered when real fluid-block wiring lands)
describe("TankInventory", () => {
  it("コンポーネントとして存在する", () => {
    expect(TankInventory).toBeTypeOf("function");
  });
});

describe("blockSlotRightClickPayload", () => {
  it("grab 保持時は block スロットへ1個置く", () => {
    expect(blockSlotRightClickPayload(2, 0, 0, 5)).toEqual({
      from: { area: "grab", slot: 0 },
      to: { area: "block", slot: 2 },
      count: 1,
    });
  });
  it("空手 + 2個以上は半分(切り捨て)を grab へ拾う", () => {
    expect(blockSlotRightClickPayload(0, 1, 7, 0)).toEqual(pickUpPayload(0, 3));
  });
  it("空手 + 1個は半分が0のため無操作(uGUI準拠)", () => {
    expect(blockSlotRightClickPayload(0, 1, 1, 0)).toBeNull();
  });
  it("空手 + 空スロットは無操作", () => {
    expect(blockSlotRightClickPayload(0, 0, 0, 0)).toBeNull();
  });
});

describe("blockShiftMovePayloads", () => {
  const slot = (itemId: number, count: number) => ({ itemId, count });
  it("main の同種スタック→空スロットの順に block からの配分 payload を作る", () => {
    const mainSlots = [slot(1, 98), slot(0, 0)];
    expect(blockShiftMovePayloads(4, 1, 7, mainSlots, 100)).toEqual([
      { from: { area: "block", slot: 4 }, to: { area: "main", slot: 0 }, count: 2 },
      { from: { area: "block", slot: 4 }, to: { area: "main", slot: 1 }, count: 5 },
    ]);
  });
  it("移動先が無ければ空配列", () => {
    expect(blockShiftMovePayloads(0, 1, 7, [slot(2, 5)], 100)).toEqual([]);
  });
});
