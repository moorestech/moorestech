// 選択レシピから「描くスロット」と各スロットのゴースト内容を導出する（ADR 0042 R7/R8）
// Derives which slots to draw and each slot's ghost content from the selected recipe (ADR 0042 R7/R8)
import type { MachineRecipe } from "@/bridge";
import { splitSlotIndices } from "../detailLogic";

export type GhostItem = { itemId: number; count: number };
type GhostFluid = { fluidGuid: string; amount: number };
type BoundItemSlot = { index: number; ghost: GhostItem | undefined };
type MachineSlotView = {
  inputs: BoundItemSlot[];
  outputs: BoundItemSlot[];
  fluidIndices: number[];
  fluidGhosts: (GhostFluid | undefined)[];
};

// recipe が null（レシピ0件機械）でも、レシピの品目数が実スロット数を下回るときも、
// 機械の全実スロットindexを列挙する（C5: 余剰スロットの実アイテムを不可視にしない）
// totalFluidSlots は data.fluidSlots.length（実タンク数）。レシピの液体数が実タンク数を超える
// 機械固有の余剰ケースでは、はみ出したindexのスロットを描かない（C10: 無いものは描かない）
// Every real slot index is always enumerated, whether recipe is null (recipe-less machine) or the
// recipe has fewer items than real slots (C5: don't make the surplus slot's real item invisible).
// totalFluidSlots is data.fluidSlots.length (real tank count). When a recipe needs more fluids than
// the machine actually has, the overflowing indices are simply not drawn (C10: don't draw what isn't there)
export function buildMachineSlotView(
  recipe: MachineRecipe | null,
  layout: { input: number; output: number; module: number },
  totalItemSlots: number,
  inputTankCount: number,
  totalFluidSlots: number,
): MachineSlotView {
  const { input, output } = splitSlotIndices(layout, totalItemSlots);
  const inputItems = recipe?.inputItems ?? [];
  const outputItems = recipe?.outputItems ?? [];
  // スロットi＝素材i、出力スロットj＝生産物j（サーバーの束縛と同じ規則）。
  // 対応する品目が無い実スロットにはghost無しで描く
  // Slot i = input i, output slot j = output j (the same rule the server enforces).
  // A real slot without a corresponding item is drawn with no ghost
  const inputs = input.map((index, i) => ({ index, ghost: i < inputItems.length ? { itemId: inputItems[i].itemId, count: inputItems[i].count } : undefined }));
  const outputs = output.map((index, j) => ({ index, ghost: j < outputItems.length ? { itemId: outputItems[j].itemId, count: outputItems[j].count } : undefined }));

  if (recipe === null) {
    const fluidIndices = Array.from({ length: totalFluidSlots }, (_, i) => i);
    return { inputs, outputs, fluidIndices, fluidGhosts: fluidIndices.map(() => undefined) };
  }

  // 液体行は入力タンク→出力タンクの連結順（BlockDetailDtoBuilder と同順）。
  // 入力液体はinputTankCountで先に切り、出力タンク帯へ食い込ませない
  // The fluid row is inputs then outputs, matching BlockDetailDtoBuilder's concatenation order.
  // Input fluids are sliced to inputTankCount first so they never spill into the output tank range
  const fluidPairs = [
    ...recipe.inputFluids.slice(0, inputTankCount).map((fluid, i) => ({ index: i, ghost: { fluidGuid: fluid.fluidGuid, amount: fluid.amount } })),
    ...recipe.outputFluids.map((fluid, j) => ({ index: inputTankCount + j, ghost: { fluidGuid: fluid.fluidGuid, amount: fluid.amount } })),
  ].filter((pair) => pair.index < totalFluidSlots);
  const fluidIndices = fluidPairs.map((pair) => pair.index);
  const fluidGhosts = fluidPairs.map((pair) => pair.ghost);

  return { inputs, outputs, fluidIndices, fluidGhosts };
}
