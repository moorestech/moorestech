// 選択レシピから「描くスロット」と各スロットのゴースト内容を導出する（ADR 0042 R7/R8）
// Derives which slots to draw and each slot's ghost content from the selected recipe (ADR 0042 R7/R8)
import type { MachineRecipe } from "@/bridge";
import { splitSlotIndices } from "../detailLogic";

export type GhostItem = { itemId: number; count: number };
export type GhostFluid = { fluidGuid: string; amount: number };
export type BoundItemSlot = { index: number; ghost: GhostItem };
export type MachineSlotView = {
  inputs: BoundItemSlot[];
  outputs: BoundItemSlot[];
  fluidIndices: number[];
  fluidGhosts: (GhostFluid | undefined)[];
};

// totalFluidSlots は data.fluidSlots.length（実タンク数）。レシピの液体数が実タンク数を超える
// 機械固有の余剰ケースでは、はみ出したindexのスロットを描かない（C10: 無いものは描かない）
// totalFluidSlots is data.fluidSlots.length (real tank count). When a recipe needs more fluids than
// the machine actually has, the overflowing indices are simply not drawn (C10: don't draw what isn't there)
export function buildMachineSlotView(
  recipe: MachineRecipe,
  layout: { input: number; output: number; module: number },
  totalItemSlots: number,
  inputTankCount: number,
  totalFluidSlots: number,
): MachineSlotView {
  const { input, output } = splitSlotIndices(layout, totalItemSlots);
  // スロットi＝素材i、出力スロットj＝生産物j（サーバーの束縛と同じ規則）
  // Slot i = input i, output slot j = output j (the same rule the server enforces)
  const inputs = recipe.inputItems.slice(0, input.length).map((item, i) => ({ index: input[i], ghost: { itemId: item.itemId, count: item.count } }));
  const outputs = recipe.outputItems.slice(0, output.length).map((item, j) => ({ index: output[j], ghost: { itemId: item.itemId, count: item.count } }));

  // 液体行は入力タンク→出力タンクの連結順（BlockDetailDtoBuilder と同順）
  // The fluid row is inputs then outputs, matching BlockDetailDtoBuilder's concatenation order
  const fluidPairs = [
    ...recipe.inputFluids.map((fluid, i) => ({ index: i, ghost: { fluidGuid: fluid.fluidGuid, amount: fluid.amount } })),
    ...recipe.outputFluids.map((fluid, j) => ({ index: inputTankCount + j, ghost: { fluidGuid: fluid.fluidGuid, amount: fluid.amount } })),
  ].filter((pair) => pair.index < totalFluidSlots);
  const fluidIndices = fluidPairs.map((pair) => pair.index);
  const fluidGhosts = fluidPairs.map((pair) => pair.ghost);

  return { inputs, outputs, fluidIndices, fluidGhosts };
}
