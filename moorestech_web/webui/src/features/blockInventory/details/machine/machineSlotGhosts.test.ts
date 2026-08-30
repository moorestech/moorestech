import { describe, expect, it } from "vitest";
import type { MachineRecipe } from "@/bridge";
import { buildMachineSlotView } from "./machineSlotGhosts";

const recipe: MachineRecipe = {
  recipeGuid: "84000000-0000-4000-8000-000000000001",
  blockGuid: "85000000-0000-4000-8000-000000000001",
  blockId: 10, time: 7,
  inputItems: [{ itemId: 1, count: 2 }, { itemId: 5, count: 1 }],
  outputItems: [{ itemId: 2, count: 1 }],
  inputFluids: [{ fluidId: 3, fluidGuid: "86000000-0000-4000-8000-000000000001", amount: 10 }],
  outputFluids: [],
};

describe("buildMachineSlotView", () => {
  it("入力は素材数・出力は生産物数だけを統合スロット番号付きで返す", () => {
    const view = buildMachineSlotView(recipe, { input: 3, output: 3, module: 1 }, 7, 2, 3);
    expect(view.inputs).toEqual([
      { index: 0, ghost: { itemId: 1, count: 2 } },
      { index: 1, ghost: { itemId: 5, count: 1 } },
    ]);
    expect(view.outputs).toEqual([{ index: 3, ghost: { itemId: 2, count: 1 } }]);
  });

  it("液体は入力タンク→出力タンクの順でレシピ分だけ返す", () => {
    const view = buildMachineSlotView(recipe, { input: 3, output: 3, module: 1 }, 7, 2, 3);
    expect(view.fluidIndices).toEqual([0]);
    expect(view.fluidGhosts).toEqual([{ fluidGuid: "86000000-0000-4000-8000-000000000001", amount: 10 }]);
  });

  it("出力液体は入力タンク数の後ろの番号を指す", () => {
    const withOutputFluid = { ...recipe, inputFluids: [], outputFluids: [{ fluidId: 4, fluidGuid: "86000000-0000-4000-8000-000000000002", amount: 4 }] };
    const view = buildMachineSlotView(withOutputFluid, { input: 3, output: 3, module: 1 }, 7, 2, 3);
    expect(view.fluidIndices).toEqual([2]);
  });

  // C10: レシピの液体数が実タンク数を超えると data.fluidSlots[i] が undefined になり FluidSlot が例外を起こすため、
  // 実在範囲を超えるindexのスロットは描かない（ghostも一緒に落とす）
  // C10: a recipe fluid beyond the real tank count would leave data.fluidSlots[i] undefined and crash FluidSlot,
  // so an out-of-range index is dropped from the view (its ghost is dropped with it)
  it("レシピの液体数が実タンク数を超える場合ははみ出したスロットを描かない", () => {
    const withOutputFluid = { ...recipe, inputFluids: [], outputFluids: [{ fluidId: 4, fluidGuid: "86000000-0000-4000-8000-000000000002", amount: 4 }] };
    // inputTankCount=2 なので出力液体はindex 2を指すが、実タンクは2本(index 0,1)しかない
    // inputTankCount=2 points the output fluid at index 2, but the machine only has 2 real tanks (index 0,1)
    const view = buildMachineSlotView(withOutputFluid, { input: 3, output: 3, module: 1 }, 7, 2, 2);
    expect(view.fluidIndices).toEqual([]);
    expect(view.fluidGhosts).toEqual([]);
  });
});
