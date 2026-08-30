import { describe, expect, it } from "vitest";
import type { MachineRecipe } from "@/bridge";
import { boundMachineInputSlotsForItem, buildMachineSlotView } from "./machineSlotGhosts";

const layout = { input: 3, output: 3, module: 1, inputTank: 2 };

const recipe: MachineRecipe = {
  recipeGuid: "84000000-0000-4000-8000-000000000001",
  blockGuid: "85000000-0000-4000-8000-000000000001",
  blockId: 10, time: 7,
  inputItems: [{ itemId: 1, count: 2 }, { itemId: 5, count: 1 }],
  outputItems: [{ itemId: 2, count: 1 }],
  inputFluids: [{ fluidId: 3, fluidGuid: "86000000-0000-4000-8000-000000000001", amount: 10 }],
  outputFluids: [],
};

const emptySlot = { itemId: 0, count: 0 };
// input(0,1,2)+output(3,4,5)+module(6) の7実スロット。既定は全スロット空
// 7 real slots: input(0,1,2)+output(3,4,5)+module(6). Empty by default
const emptyItemSlots = () => Array.from({ length: 7 }, () => ({ ...emptySlot }));

describe("buildMachineSlotView", () => {
  it("レシピ分のスロットだけを列挙し、対応する位置にghostを返す（R7・2026-08-30裁定）", () => {
    const view = buildMachineSlotView(recipe, layout, { totalItemSlots: 7, totalFluidSlots: 3 }, emptyItemSlots());
    // recipeはinputItems 2件・outputItems 1件なので、束縛外(input index2, output index4,5)は空なら描かない
    // recipe has 2 inputItems and 1 outputItem, so the unbound slots (input index2, output index4,5) are undrawn while empty
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
    const view = buildMachineSlotView(recipe, layout, { totalItemSlots: 7, totalFluidSlots: 3 }, itemSlots);
    expect(view.inputs).toEqual([
      { index: 0, ghost: { itemId: 1, count: 2 } },
      { index: 1, ghost: { itemId: 5, count: 1 } },
      { index: 2, ghost: undefined },
    ]);
  });

  it("液体は入力タンク→出力タンクの順でレシピ分だけ返す", () => {
    const view = buildMachineSlotView(recipe, layout, { totalItemSlots: 7, totalFluidSlots: 3 }, emptyItemSlots());
    expect(view.fluids).toEqual([{ index: 0, ghost: { fluidGuid: "86000000-0000-4000-8000-000000000001", amount: 10 } }]);
  });

  it("出力液体は入力タンク数の後ろの番号を指す", () => {
    const withOutputFluid = { ...recipe, inputFluids: [], outputFluids: [{ fluidId: 4, fluidGuid: "86000000-0000-4000-8000-000000000002", amount: 4 }] };
    const view = buildMachineSlotView(withOutputFluid, layout, { totalItemSlots: 7, totalFluidSlots: 3 }, emptyItemSlots());
    expect(view.fluids.map((f) => f.index)).toEqual([2]);
  });

  // C10: レシピの液体数が実タンク数を超えると data.fluidSlots[i] が undefined になり FluidSlot が例外を起こすため、
  // 実在範囲を超えるindexのスロットは描かない（ghostも一緒に落とす）
  // C10: a recipe fluid beyond the real tank count would leave data.fluidSlots[i] undefined and crash FluidSlot,
  // so an out-of-range index is dropped from the view (its ghost is dropped with it)
  it("レシピの液体数が実タンク数を超える場合ははみ出したスロットを描かない", () => {
    const withOutputFluid = { ...recipe, inputFluids: [], outputFluids: [{ fluidId: 4, fluidGuid: "86000000-0000-4000-8000-000000000002", amount: 4 }] };
    // inputTank=2 なので出力液体はindex 2を指すが、実タンクは2本(index 0,1)しかない
    // inputTank=2 points the output fluid at index 2, but the machine only has 2 real tanks (index 0,1)
    const view = buildMachineSlotView(withOutputFluid, layout, { totalItemSlots: 7, totalFluidSlots: 2 }, emptyItemSlots());
    expect(view.fluids).toEqual([]);
  });

  it("recipeがnullなら全実スロットをghost無しで返す（レシピ0件機械・R11現状維持）", () => {
    const view = buildMachineSlotView(null, layout, { totalItemSlots: 7, totalFluidSlots: 2 }, emptyItemSlots());
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
    expect(boundMachineInputSlotsForItem(recipe, layout, 7, 1)).toEqual([0]);
    expect(boundMachineInputSlotsForItem(recipe, layout, 7, 5)).toEqual([1]);
  });

  it("束縛されないitemIdは空配列を返す", () => {
    expect(boundMachineInputSlotsForItem(recipe, layout, 7, 999)).toEqual([]);
  });
});
