// ホストが配信するスロット束縛から「描くスロット」と各スロットのゴースト内容を導出する（ADR 0042 R7/R8、2026-08-30裁定）
// Derives which slots to draw and each slot's ghost content from the host-published slot binding (ADR 0042 R7/R8, 2026-08-30 ruling)
import type { FluidSlotData, MachineDetailData, SlotData } from "@/bridge";
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

function hasRealItem(slot: SlotData | undefined): boolean {
  return slot !== undefined && slot.itemId !== 0 && slot.count > 0;
}

function hasRealFluid(slot: FluidSlotData | undefined): boolean {
  return slot !== undefined && slot.kind === "filled" && slot.amount > 0;
}

// 束縛スロットを帯の順に描き、続けて束縛外だが中身が残っている実スロットを描く（サーバー返却で戻しきれなかった残置分）。
// 中身の無い束縛外スロットは描かない（レシピ分のスロットだけを描くR7を保つ）
// Draw the bound slots in band order, then any unbound slot that still holds something (leftovers the server refund could not return).
// An empty unbound slot stays undrawn, preserving R7's "recipe-count slots only"
function buildBoundItemBand(bandIndices: number[], ghosts: Map<number, GhostItem>, itemSlots: SlotData[]): BoundItemSlot[] {
  const bound = bandIndices.filter((index) => ghosts.has(index)).map((index) => ({ index, ghost: ghosts.get(index) }));
  const leftovers = bandIndices.filter((index) => !ghosts.has(index) && hasRealItem(itemSlots[index])).map((index) => ({ index, ghost: undefined }));
  return [...bound, ...leftovers];
}

function buildBoundFluidBand(ghosts: Map<number, GhostFluid>, fluidSlots: FluidSlotData[]): BoundFluidSlot[] {
  const indices = fluidSlots.map((_, index) => index);
  const bound = indices.filter((index) => ghosts.has(index)).map((index) => ({ index, ghost: ghosts.get(index) }));
  const leftovers = indices.filter((index) => !ghosts.has(index) && hasRealFluid(fluidSlots[index])).map((index) => ({ index, ghost: undefined }));
  return [...bound, ...leftovers];
}

// 束縛が1件も無い機械（レシピ0件・未選択）は従来どおり全実スロットを描く（R11: 現状維持）。
// 束縛があるときは束縛スロット＋中身の残る束縛外スロットだけを描く（R7 + 2026-08-30裁定）
// A machine with no binding at all (recipe-less or unselected) still enumerates every real slot (R11: unchanged).
// With a binding, only the bound slots plus any occupied unbound slot are drawn (R7 + 2026-08-30 ruling)
export function buildMachineSlotView(machine: MachineDetailData, itemSlots: SlotData[], fluidSlots: FluidSlotData[]): MachineSlotView {
  const { input, output } = splitSlotIndices(machine.slotLayout, itemSlots.length);

  if (machine.slotBindings.length === 0 && machine.tankBindings.length === 0) {
    return {
      inputs: input.map((index) => ({ index, ghost: undefined })),
      outputs: output.map((index) => ({ index, ghost: undefined })),
      fluids: fluidSlots.map((_, index) => ({ index, ghost: undefined })),
    };
  }

  const itemGhosts = new Map(machine.slotBindings.map((binding) => [binding.slot, { itemId: binding.itemId, count: binding.count }]));
  const fluidGhosts = new Map(machine.tankBindings.map((binding) => [binding.tank, { fluidGuid: binding.fluidGuid, amount: binding.amount }]));

  return {
    inputs: buildBoundItemBand(input, itemGhosts, itemSlots),
    outputs: buildBoundItemBand(output, itemGhosts, itemSlots),
    fluids: buildBoundFluidBand(fluidGhosts, fluidSlots),
  };
}

// 入力帯で itemId に束縛されるスロットindexを返す。
// Shift移動（block_inventory.move_item）の宛先を束縛先だけへ絞るために使う
// Slot indices bound to itemId within the input band.
// Used to narrow Shift-move (block_inventory.move_item) destinations to bound slots only
export function boundMachineInputSlotsForItem(machine: MachineDetailData, totalItemSlots: number, itemId: number): number[] {
  const { input } = splitSlotIndices(machine.slotLayout, totalItemSlots);
  const inputBand = new Set(input);
  return machine.slotBindings.filter((binding) => inputBand.has(binding.slot) && binding.itemId === itemId).map((binding) => binding.slot);
}
