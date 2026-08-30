// 選択レシピから「描くスロット」と各スロットのゴースト内容を導出する（ADR 0042 R7/R8、2026-08-30裁定）
// Derives which slots to draw and each slot's ghost content from the selected recipe (ADR 0042 R7/R8, 2026-08-30 ruling)
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
type ItemSlotContent = { itemId: number; count: number };

function hasRealItem(slot: ItemSlotContent | undefined): boolean {
  return slot !== undefined && slot.itemId !== 0 && slot.count > 0;
}

// レシピ分のスロットindexに、束縛外だが中身が残っている実スロットindexを加える（サーバー返却で戻しきれなかった残置分）。
// 中身の無い束縛外スロットは描かない（レシピ分のスロットだけを描くR7を保つ）
// Add unbound real-slot indices that still hold an item (leftovers the server refund could not fully return)
// to the recipe-count slots. An empty unbound slot stays undrawn, preserving R7's "recipe-count slots only"
function withOccupiedLeftovers(boundIndices: number[], candidateIndices: number[], itemSlots: ItemSlotContent[]): number[] {
  const bound = new Set(boundIndices);
  const leftovers = candidateIndices.filter((index) => !bound.has(index) && hasRealItem(itemSlots[index]));
  return [...boundIndices, ...leftovers];
}

// 束縛範囲(レシピ品目数と実スロット数の小さい方)＋中身のある束縛外スロットだけを描く。品目に対応する位置だけghostを持つ
// Draw the bound range (min of recipe item count and real slot count) plus any occupied unbound slot; only bound positions carry a ghost
function buildBoundItemSlots(realIndices: number[], recipeItems: { itemId: number; count: number }[], itemSlots: ItemSlotContent[]): BoundItemSlot[] {
  const boundCount = Math.min(recipeItems.length, realIndices.length);
  const boundIndices = realIndices.slice(0, boundCount);
  const drawnIndices = withOccupiedLeftovers(boundIndices, realIndices, itemSlots);
  return drawnIndices.map((index) => {
    const boundPosition = boundIndices.indexOf(index);
    return { index, ghost: boundPosition === -1 ? undefined : { itemId: recipeItems[boundPosition].itemId, count: recipeItems[boundPosition].count } };
  });
}

// recipe が null（レシピ0件機械）は従来どおり機械の全実スロットindexを列挙する（R11: 現状維持）。
// recipe があるときはレシピ分のスロット＋中身の残る束縛外スロットだけを描く（R7 + 2026-08-30裁定）。
// totalFluidSlots は実タンク数。レシピの液体数が実タンク数を超える機械固有の余剰ケースでは、
// はみ出したindexのスロットを描かない（C10: 無いものは描かない）
// A null recipe (recipe-less machine) still enumerates every real slot index (R11: unchanged).
// With a recipe, draw only the recipe-count slots plus any unbound slot that still holds an item (R7 + 2026-08-30 ruling).
// totalFluidSlots is the real tank count. When a recipe needs more fluids than the machine actually
// has, the overflowing indices are simply not drawn (C10: don't draw what isn't there)
export function buildMachineSlotView(recipe: MachineRecipe | null, layout: MachineItemSlotLayout, counts: MachineSlotCounts, itemSlots: ItemSlotContent[]): MachineSlotView {
  const { totalItemSlots, totalFluidSlots } = counts;
  const { input, output } = splitSlotIndices(layout, totalItemSlots);

  if (recipe === null) {
    const inputs = input.map((index) => ({ index, ghost: undefined }));
    const outputs = output.map((index) => ({ index, ghost: undefined }));
    const fluids = Array.from({ length: totalFluidSlots }, (_, i) => ({ index: i, ghost: undefined }));
    return { inputs, outputs, fluids };
  }

  const inputs = buildBoundItemSlots(input, recipe.inputItems, itemSlots);
  const outputs = buildBoundItemSlots(output, recipe.outputItems, itemSlots);

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
