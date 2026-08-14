// ホットバーD&Dの純粋ロジック
// Pure hotbar-D&D logic: resolves a drag source/target pair into the action to dispatch

// D&Dの共通端点。枠外はoutside
// Shared endpoint type for drag source/target; a drop outside any slot is "outside"
export type DragEndpoint =
  | { kind: "buildMenuEntry"; id: string }
  | { kind: "hotbarSlot"; index: number }
  | { kind: "outside" };

type HotbarDropAction =
  | { type: "hotbar.assign"; payload: { slot: number; id: string } }
  | { type: "hotbar.swap"; payload: { from: number; to: number } }
  | { type: "hotbar.clear"; payload: { slot: number } };

// 枠の組み合わせをassign/swap/clearへ
// buildMenuEntry→slot is assign, slot→slot is swap, slot→outside is clear; any other pairing is an invalid drop (null)
export function resolveDropAction(source: DragEndpoint, target: DragEndpoint): HotbarDropAction | null {
  if (source.kind === "buildMenuEntry") {
    if (target.kind !== "hotbarSlot") return null;
    return { type: "hotbar.assign", payload: { slot: target.index, id: source.id } };
  }

  if (source.kind === "hotbarSlot") {
    if (target.kind === "hotbarSlot") {
      // 自分自身へのdropは無視
      // A drop onto itself changes nothing, so ignore it
      if (target.index === source.index) return null;
      return { type: "hotbar.swap", payload: { from: source.index, to: target.index } };
    }
    if (target.kind === "outside") {
      return { type: "hotbar.clear", payload: { slot: source.index } };
    }
    return null;
  }

  return null;
}
