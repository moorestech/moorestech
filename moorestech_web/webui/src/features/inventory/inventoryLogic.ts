import type { SlotData } from "@/bridge/contract/payloadTypes";

// Shift+クリック直接移動の配分計画（uGUI PlayerInventoryDirectMover 準拠）
// Direct-move allocation plan for Shift+click (mirrors uGUI PlayerInventoryDirectMover)
export type PlannedMove = { slot: number; count: number };

// 同種スタック(空きあり)へ順に詰め、残りを最初の空スロットへ置く。入り切らない分は配分しない
// Fill same-item stacks with room in order, then drop the rest on the first empty slot; overflow stays unplanned
// maxStack が undefined（マスタ未ロード）なら同種探索をスキップし空スロットのみ使う
// When maxStack is undefined (master unloaded) skip the same-item search and use empty slots only
export function planDirectMoves(
  sourceCount: number,
  itemId: number,
  maxStack: number | undefined,
  targetSlots: SlotData[],
): PlannedMove[] {
  const moves: PlannedMove[] = [];
  let remaining = sourceCount;
  if (maxStack !== undefined) {
    for (let i = 0; i < targetSlots.length && remaining > 0; i++) {
      const target = targetSlots[i];
      if (target.count === 0 || target.itemId !== itemId || maxStack <= target.count) continue;
      const count = Math.min(remaining, maxStack - target.count);
      moves.push({ slot: i, count });
      remaining -= count;
    }
  }
  // ソースは1スタック分なので、残り全量は空スロット1つで収まる
  // The source is a single stack, so one empty slot always fits the remainder
  if (remaining > 0) {
    const empty = targetSlots.findIndex((s) => s.count === 0);
    if (empty >= 0) moves.push({ slot: empty, count: remaining });
  }
  return moves;
}
