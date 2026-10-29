import type { BlockInventoryData } from "@/bridge";
import type { BlockSlotRestriction } from "@/shared/itemMove";
import { boundMachineInputSlotsForItem } from "@/features/blockInventory/details/machine/machineSlotGhosts";
import { splitSlotIndices } from "@/features/blockInventory/details/detailLogic";

type InputSlotBinding = { slot: number; acceptedItemIds: number[] };

// Shift配分の宛先候補をブロック種別ごとに確定する。受け付け情報はホスト配信の束縛が正本で、ここで規則を再導出しない
// Settles Shift-allocation candidates per block type; the host-published bindings are the source of truth and no rule is re-derived here
export function resolveBlockSlotRestriction(block: BlockInventoryData | null, itemId: number): BlockSlotRestriction {
  if (!block?.open || block.source !== "block") return { kind: "unrestricted" };

  // 発射台と宇宙エレベーターは入れ口ごとに受け付けるアイテムが決まり、出力枠は投入先にしない
  // Launch pads and space elevators fix the accepted items per inlet, and their output slots are never insertion targets
  if (block.blockType === "RocketLaunchPad") return boundByInputSlotBindings(block.rocketLaunchPad?.slotBindings, itemId);
  if (block.blockType === "SpaceElevatorBase") return boundByInputSlotBindings(block.spaceElevator?.slotBindings, itemId);
  return resolveMachineSlotRestriction(block, itemId);
}

// 束縛が未着の間は候補なしにし、無制限へ倒して受け付けない枠へ送らない（パネルは詳細未着の表示のまま）
// While bindings are pending there are no candidates; never fall back to unrestricted and send into refusing slots (the panel keeps showing its pending detail)
function boundByInputSlotBindings(bindings: InputSlotBinding[] | undefined, itemId: number): BlockSlotRestriction {
  if (!bindings) return { kind: "bound", candidateIndices: [] };
  return { kind: "bound", candidateIndices: bindings.filter((binding) => binding.acceptedItemIds.includes(itemId)).map((binding) => binding.slot) };
}

// 機械の入力帯だけを束縛で絞り、出力帯・モジュール帯は候補に残す（束縛はホスト配信のslotBindingsが正本）。
// 機械以外（チェスト等）は無制限
// Only the machine's input band is narrowed by the binding; the output and module bands stay candidates
// (the host-published slotBindings are the source of truth). Non-machines (chests etc.) stay unrestricted
function resolveMachineSlotRestriction(block: Extract<BlockInventoryData, { source: "block" }>, itemId: number): BlockSlotRestriction {
  if (!block.machine) return { kind: "unrestricted" };
  const machine = block.machine;
  if (machine.slotBindings.length === 0) return { kind: "unrestricted" };

  const { output, module } = splitSlotIndices(machine.slotLayout, block.itemSlots.length);
  const boundInputs = boundMachineInputSlotsForItem(machine, block.itemSlots.length, itemId);
  return { kind: "bound", candidateIndices: [...boundInputs, ...output, ...module] };
}
