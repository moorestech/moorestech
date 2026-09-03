import { describe, expect, it } from "vitest";
import type { FluidSlotData, MachineDetailData } from "@/bridge";
import { boundMachineInputSlotsForItem, buildMachineSlotView } from "./machineSlotGhosts";

const layout = { input: 3, output: 3, module: 1, inputTank: 2 };

// ホストが配信する束縛。input(0,1)へ素材2件、output(3)へ生産物1件、tank(0)へ入力液体1件
// The host-published binding: 2 inputs on slots 0 and 1, 1 output on slot 3, 1 input fluid on tank 0
const machine: MachineDetailData = {
  recipeGuid: "84000000-0000-4000-8000-000000000001",
  selectedRecipeGuid: "84000000-0000-4000-8000-000000000001",
  blockGuid: "85000000-0000-4000-8000-000000000001",
  recipeTime: 7,
  outputItems: [{ itemId: 2, count: 1 }],
  currentState: "idle",
  currentPower: 0,
  requestPower: 0,
  slotLayout: layout,
  slotBindings: [
    { slot: 0, itemId: 1, count: 2 },
    { slot: 1, itemId: 5, count: 1 },
    { slot: 3, itemId: 2, count: 1 },
  ],
  tankBindings: [{ tank: 0, fluidGuid: "86000000-0000-4000-8000-000000000001", amount: 10 }],
};

const emptySlot = { itemId: 0, count: 0 };
// input(0,1,2)+output(3,4,5)+module(6) の7実スロット。既定は全スロット空
// 7 real slots: input(0,1,2)+output(3,4,5)+module(6). Empty by default
const emptyItemSlots = () => Array.from({ length: 7 }, () => ({ ...emptySlot }));
const emptyFluidSlots = (count: number): FluidSlotData[] => Array.from({ length: count }, () => ({ kind: "empty" as const, capacity: 100 }));

describe("buildMachineSlotView", () => {
  it("束縛スロットだけを列挙し、対応する位置にghostを返す（R7・2026-08-30裁定）", () => {
    const view = buildMachineSlotView(machine, emptyItemSlots(), emptyFluidSlots(3));
    // 束縛は input 2件・output 1件なので、束縛外(input index2, output index4,5)は空なら描かない
    // The binding covers 2 inputs and 1 output, so the unbound slots (input index2, output index4,5) stay undrawn while empty
    expect(view.inputs).toEqual([
      { index: 0, ghost: { itemId: 1, count: 2 } },
      { index: 1, ghost: { itemId: 5, count: 1 } },
    ]);
    expect(view.outputs).toEqual([{ index: 3, ghost: { itemId: 2, count: 1 } }]);
  });

  it("束縛外スロットに実アイテムがある場合だけそのスロットも描く（ghost無し・戻しきれなかった残置分）", () => {
    const itemSlots = emptyItemSlots();
    // input index2(束縛外)に返却しきれなかった旧素材が残っている想定
    // input index2 (unbound) holds a leftover material the refund could not fully return
    itemSlots[2] = { itemId: 9, count: 4 };
    const view = buildMachineSlotView(machine, itemSlots, emptyFluidSlots(3));
    expect(view.inputs).toEqual([
      { index: 0, ghost: { itemId: 1, count: 2 } },
      { index: 1, ghost: { itemId: 5, count: 1 } },
      { index: 2, ghost: undefined },
    ]);
  });

  it("液体は束縛されたタンクだけを返す", () => {
    const view = buildMachineSlotView(machine, emptyItemSlots(), emptyFluidSlots(3));
    expect(view.fluids).toEqual([{ index: 0, ghost: { fluidGuid: "86000000-0000-4000-8000-000000000001", amount: 10 } }]);
  });

  it("出力液体は入力タンク数の後ろの番号を指す", () => {
    const withOutputFluid: MachineDetailData = { ...machine, tankBindings: [{ tank: 2, fluidGuid: "86000000-0000-4000-8000-000000000002", amount: 4 }] };
    const view = buildMachineSlotView(withOutputFluid, emptyItemSlots(), emptyFluidSlots(3));
    expect(view.fluids.map((f) => f.index)).toEqual([2]);
  });

  it("束縛外でも中身の残っているタンクは描く（液体側の残置救済）", () => {
    const fluidSlots = emptyFluidSlots(3);
    fluidSlots[1] = { kind: "filled", fluidGuid: "86000000-0000-4000-8000-000000000003", amount: 20, capacity: 100 };
    const view = buildMachineSlotView(machine, emptyItemSlots(), fluidSlots);
    expect(view.fluids).toEqual([
      { index: 0, ghost: { fluidGuid: "86000000-0000-4000-8000-000000000001", amount: 10 } },
      { index: 1, ghost: undefined },
    ]);
  });

  it("束縛が無い機械は全実スロットをghost無しで返す（レシピ0件機械・R11現状維持）", () => {
    const unbound: MachineDetailData = { ...machine, slotBindings: [], tankBindings: [] };
    const view = buildMachineSlotView(unbound, emptyItemSlots(), emptyFluidSlots(2));
    expect(view.inputs).toEqual([
      { index: 0, ghost: undefined },
      { index: 1, ghost: undefined },
      { index: 2, ghost: undefined },
    ]);
    expect(view.outputs).toEqual([
      { index: 3, ghost: undefined },
      { index: 4, ghost: undefined },
      { index: 5, ghost: undefined },
    ]);
    expect(view.fluids).toEqual([
      { index: 0, ghost: undefined },
      { index: 1, ghost: undefined },
    ]);
  });
});

describe("boundMachineInputSlotsForItem", () => {
  // Warning回帰: Shift移動の宛先を束縛先だけへ絞る材料。重複itemIdでも束縛される全スロットを返す
  // Warning regression: material for narrowing Shift-move destinations to bound slots only; returns every bound slot even for a duplicated itemId
  it("素材のitemIdが束縛される入力スロットindexを返す", () => {
    expect(boundMachineInputSlotsForItem(machine, 7, 1)).toEqual([0]);
    expect(boundMachineInputSlotsForItem(machine, 7, 5)).toEqual([1]);
  });

  it("出力帯の束縛は入力帯の候補に混ざらない", () => {
    expect(boundMachineInputSlotsForItem(machine, 7, 2)).toEqual([]);
  });

  it("束縛されないitemIdは空配列を返す", () => {
    expect(boundMachineInputSlotsForItem(machine, 7, 999)).toEqual([]);
  });
});
