import { describe, it, expect } from "vitest";
import { planDirectMoves } from "./inventoryLogic";

const slot = (itemId: number, count: number) => ({ itemId, count });

describe("planDirectMoves", () => {
  it("同種スタックの空きへ順に詰める（複数スタック配分）", () => {
    const targets = [slot(1, 98), slot(2, 5), slot(0, 0), slot(1, 10)];
    expect(planDirectMoves(7, 1, 100, targets)).toEqual([
      { slot: 0, count: 2 },
      { slot: 3, count: 5 },
    ]);
  });

  it("スタックで吸収しきれない残りは最初の空スロットへ", () => {
    const targets = [slot(1, 98), slot(0, 0)];
    expect(planDirectMoves(7, 1, 100, targets)).toEqual([
      { slot: 0, count: 2 },
      { slot: 1, count: 5 },
    ]);
  });

  it("同種スタックに全量入るなら空スロットは使わない", () => {
    const targets = [slot(1, 10), slot(0, 0)];
    expect(planDirectMoves(7, 1, 100, targets)).toEqual([{ slot: 0, count: 7 }]);
  });

  it("同種が無ければ最初の空スロットへ全量", () => {
    const targets = [slot(2, 3), slot(0, 0), slot(0, 0)];
    expect(planDirectMoves(7, 1, 100, targets)).toEqual([{ slot: 1, count: 7 }]);
  });

  it("満杯スタックは飛ばす", () => {
    const targets = [slot(1, 100), slot(0, 0)];
    expect(planDirectMoves(7, 1, 100, targets)).toEqual([{ slot: 1, count: 7 }]);
  });

  it("maxStack 不明時（マスタ未ロード）は同種探索をスキップし空スロットのみ使う", () => {
    const targets = [slot(1, 10), slot(0, 0)];
    expect(planDirectMoves(7, 1, undefined, targets)).toEqual([{ slot: 1, count: 7 }]);
  });

  it("移動先が無ければ空配列、空スロットが無ければ部分配分のみ返す", () => {
    expect(planDirectMoves(7, 1, 100, [slot(2, 5)])).toEqual([]);
    expect(planDirectMoves(7, 1, 100, [slot(1, 98), slot(2, 5)])).toEqual([{ slot: 0, count: 2 }]);
  });
});
