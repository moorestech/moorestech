// 選択レシピから「描くスロット」と各スロットのゴースト内容を導出する（ADR 0042 R7/R8）
// Derives which slots to draw and each slot's ghost content from the selected recipe (ADR 0042 R7/R8)
import type { MachineRecipe } from "@/bridge";
import { splitSlotIndices } from "../detailLogic";

export type GhostItem = { itemId: number; count: number };
type GhostFluid = { fluidGuid: string; amount: number };
type BoundItemSlot = { index: number; ghost: GhostItem | undefined };
type BoundFluidSlot = { index: number; ghost: GhostFluid | undefined };
type MachineSlotView = {
  inputs: BoundItemSlot[];
  outputs: BoundItemSlot[];
  fluids: BoundFluidSlot[];
};
type MachineItemSlotLayout = { input: number; output: number; module: number; inputTank: number };
type MachineSlotCounts = { totalItemSlots: number; totalFluidSlots: number };

// recipe が null（レシピ0件機械）でも、レシピの品目数が実スロット数を下回るときも、
// 機械の全実スロットindexを列挙する（C5: 余剰スロットの実アイテムを不可視にしない）
// totalFluidSlots は実タンク数。レシピの液体数が実タンク数を超える機械固有の余剰ケースでは、
// はみ出したindexのスロットを描かない（C10: 無いものは描かない）
// Every real slot index is always enumerated, whether recipe is null (recipe-less machine) or the
// recipe has fewer items than real slots (C5: don't make the surplus slot's real item invisible).
// totalFluidSlots is the real tank count. When a recipe needs more fluids than the machine actually
// has, the overflowing indices are simply not drawn (C10: don't draw what isn't there)
export function buildMachineSlotView(recipe: MachineRecipe | null, layout: MachineItemSlotLayout, counts: MachineSlotCounts): MachineSlotView {
  const { totalItemSlots, totalFluidSlots } = counts;
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
    const fluids = Array.from({ length: totalFluidSlots }, (_, i) => ({ index: i, ghost: undefined }));
    return { inputs, outputs, fluids };
  }

  // 液体行は入力タンク→出力タンクの連結順（BlockDetailDtoBuilder と同順）。
  // 入力液体はinputTankで先に切り、出力タンク帯へ食い込ませない
  // The fluid row is inputs then outputs, matching BlockDetailDtoBuilder's concatenation order.
  // Input fluids are sliced to inputTank first so they never spill into the output tank range
  const fluids = [
    ...recipe.inputFluids.slice(0, layout.inputTank).map((fluid, i) => ({ index: i, ghost: { fluidGuid: fluid.fluidGuid, amount: fluid.amount } })),
    ...recipe.outputFluids.map((fluid, j) => ({ index: layout.inputTank + j, ghost: { fluidGuid: fluid.fluidGuid, amount: fluid.amount } })),
  ].filter((pair) => pair.index < totalFluidSlots);

  return { inputs, outputs, fluids };
}

// 選択レシピが束縛する入力スロットindexのうちitemIdに束縛されるものを返す。
// Shift移動（block_inventory.move_item）の宛先を束縛先だけへ絞るために使う
// Slot indices the selected recipe binds to itemId, restricted to inputs.
// Used to narrow Shift-move (block_inventory.move_item) destinations to bound slots only
export function boundMachineInputSlotsForItem(recipe: MachineRecipe, layout: MachineItemSlotLayout, totalItemSlots: number, itemId: number): number[] {
  const { input } = splitSlotIndices(layout, totalItemSlots);
  const bound: number[] = [];
  recipe.inputItems.forEach((item, i) => {
    if (item.itemId === itemId && i < input.length) bound.push(input[i]);
  });
  return bound;
}
