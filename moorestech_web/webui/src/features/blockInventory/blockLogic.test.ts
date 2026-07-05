import { describe, it, expect, vi } from "vitest";

// 解決対象コンポーネントは BlockItemGrid 経由で webSocketClient を読み込む。
// import 時に location.host を触るため node 環境では stub する
// The resolved components import webSocketClient via BlockItemGrid, which touches location.host at import;
// stub it so this node-env test can load the component tree
vi.mock("@/bridge/webSocketClient", () => ({ sendAction: vi.fn() }));

import { pickUpPayload, placePayload, resolveBlockComponent } from "./blockLogic";
import ChestInventory from "./ChestInventory";
import TankInventory from "./TankInventory";
import GenericBlockInventory from "./GenericBlockInventory";

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
});

// TankInventory は専用 UI として温存（実流体ブロック配線時に再登録する想定）
// TankInventory is kept as a dedicated UI (to be re-registered when real fluid-block wiring lands)
describe("TankInventory", () => {
  it("コンポーネントとして存在する", () => {
    expect(TankInventory).toBeTypeOf("function");
  });
});
