import type { PlayerInventoryData, SlotData, SlotRef } from "@/bridge";
import { planDirectMoves } from "./planDirectMoves";
import type { PlannedAction } from "./plannedAction";

export const GRAB: SlotRef = { area: "grab", slot: 0 };

// プレイヤースロット操作の判定材料。blockItemSlots はブロックUI開時のみ非null（Shift配分の宛先になる）
// blockSlotRestriction は「無制限」か「候補index列」かの2択を呼び出し側が値で確定させて渡す（判定は具体側の責務）
// Inputs for player-slot decisions; blockItemSlots is non-null only while a block UI is open (Shift target).
// blockSlotRestriction is settled by the caller as either unrestricted or an explicit candidate list (the decision belongs to the concrete side)
export type BlockSlotRestriction = { kind: "unrestricted" } | { kind: "bound"; candidateIndices: number[] };

export type PlayerSlotContext = {
  inventory: PlayerInventoryData;
  maxStack: number | undefined;
  blockItemSlots: SlotData[] | null;
  blockSlotRestriction: BlockSlotRestriction;
};

// 左クリックの帰結。actionsは送信するプラン、beginSplitDragはドラッグ開始の合図、noAcceptingBlockSlotは受け付ける枠が無く送らなかった拒否
// Left-click outcome: actions to dispatch, beginSplitDrag to start the drag, or noAcceptingBlockSlot when no slot accepts the item and nothing is sent
export type PlayerLeftClickPlan = { kind: "actions"; actions: PlannedAction[] } | { kind: "beginSplitDrag" } | { kind: "noAcceptingBlockSlot" };

// 左クリック: grab保持中の空スロットはsplitDrag開始 / 全量置き / Shiftなら配分移動 / 中身ありなら全量掴み
// Left click: an empty slot while holding grab begins split-drag / place-all / allocate on Shift / pick the whole stack when filled
export function planPlayerLeftClick(ref: SlotRef, slot: SlotData, shiftKey: boolean, ctx: PlayerSlotContext): PlayerLeftClickPlan {
  const grabCount = ctx.inventory.grab.count;
  if (grabCount > 0) {
    if (!shiftKey && slot.count === 0) return { kind: "beginSplitDrag" };
    return { kind: "actions", actions: [{ type: "inventory.move_item", payload: { from: GRAB, to: ref, count: grabCount } }] };
  }
  if (slot.count === 0) return { kind: "actions", actions: [] };
  if (shiftKey) return planShiftMove(ref, slot, ctx);
  return { kind: "actions", actions: [{ type: "inventory.move_item", payload: { from: ref, to: GRAB, count: slot.count } }] };
}

// 右クリック: grab保持なら1個置き / 空手なら inventory.split（半分掴みはホスト計算。stale な client 数量に依存しない）
// Right click: place one while holding grab / inventory.split empty-handed (the host computes the half; no stale client count)
export function planPlayerRightClick(ref: SlotRef, slot: SlotData, grabCount: number): PlannedAction[] {
  if (grabCount > 0) return [{ type: "inventory.move_item", payload: { from: GRAB, to: ref, count: 1 } }];
  if (slot.count === 0) return [];
  return [{ type: "inventory.split", payload: { from: ref } }];
}

// ダブルクリック: 収集先（grab かクリックスロットか）はホストが自身の grab 状態で決める
// Double click: the host decides the target (grab vs clicked slot) from its own grab state
export function planPlayerDoubleClick(ref: SlotRef): PlannedAction[] {
  return [{ type: "inventory.collect", payload: { slot: ref } }];
}

// Shift+クリックでblockへ配分
// Shift-click: allocate into the block while its UI is open; shift from equipment returns the stack to the main area (the old main<->hotbar swap is gone)
function planShiftMove(from: SlotRef, slot: SlotData, ctx: PlayerSlotContext): PlayerLeftClickPlan {
  const blockItemSlots = ctx.blockItemSlots;
  if (blockItemSlots) {
    // 束縛で受け付ける枠が1つも無いのは無操作ではなく拒否。呼び出し側が理由を出せるよう区別して返す
    // A bound restriction with no accepting slot is a refusal, not a no-op; return it distinctly so the caller can surface why
    if (ctx.blockSlotRestriction.kind === "bound" && ctx.blockSlotRestriction.candidateIndices.length === 0) return { kind: "noAcceptingBlockSlot" };
    // 候補が束縛で絞られている場合はそこだけを宛先にする。move_itemはスロット固定のswapのためサーバーが
    // 束縛外を無言で拒否し、全スロットへ配分すると通信上は成功扱いで何も起きない
    // A bound restriction narrows the destinations: move_item swaps a fixed slot and the server silently
    // rejects an unbound one, so spreading across every slot looks like success but does nothing
    const candidateIndices = ctx.blockSlotRestriction.kind === "bound"
      ? ctx.blockSlotRestriction.candidateIndices
      : blockItemSlots.map((_, i) => i);
    const candidateSlots = candidateIndices.map((i) => blockItemSlots[i]);
    const blockMoves: PlannedAction[] = planDirectMoves(slot.count, slot.itemId, ctx.maxStack, candidateSlots).map((m) => ({
      type: "block_inventory.move_item",
      payload: { from, to: { area: "block", slot: candidateIndices[m.slot] }, count: m.count },
    }));
    return { kind: "actions", actions: blockMoves };
  }
  if (from.area !== "equipment") return { kind: "actions", actions: [] };
  const mainMoves: PlannedAction[] = planDirectMoves(slot.count, slot.itemId, ctx.maxStack, ctx.inventory.mainSlots).map((m) => ({
    type: "inventory.move_item",
    payload: { from, to: { area: "main", slot: m.slot }, count: m.count },
  }));
  return { kind: "actions", actions: mainMoves };
}
