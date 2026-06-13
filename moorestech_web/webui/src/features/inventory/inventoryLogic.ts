import type { SlotData } from "@/bridge/payloadTypes";

// Shift+クリックの移動先 index を決める。同種スタック(空きあり)優先→空スロット→無ければ -1。
// maxStack が undefined（マスタ未ロード）なら同種探索を飛ばし空スロットのみ。
// Decide the direct-move target index: same-item stack with room first, then empty, else -1.
// When maxStack is undefined (master unloaded) skip the same-item search and use empty only.
export function resolveDirectMoveTarget(
  targetSlots: SlotData[],
  itemId: number,
  maxStack: number | undefined,
): number {
  const stackable =
    maxStack === undefined ? -1 : targetSlots.findIndex((s) => s.itemId === itemId && s.count < maxStack);
  const empty = targetSlots.findIndex((s) => s.count === 0);
  return stackable >= 0 ? stackable : empty;
}
