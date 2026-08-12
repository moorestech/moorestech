// ホットバーD&Dの純粋ロジック（ドラッグ元/先→送信アクションの解決）
// Pure hotbar-D&D logic: resolves a drag source/target pair into the action to dispatch

// ドラッグ元/先を表す共通の端点。枠外へのdropは"outside"で表す
// Shared endpoint type for drag source/target; a drop outside any slot is "outside"
export type DragEndpoint =
  | { kind: "buildMenuEntry"; id: string }
  | { kind: "hotbarSlot"; index: number }
  | { kind: "outside" };

export type HotbarDropAction =
  | { type: "hotbar.assign"; payload: { slot: number; id: string } }
  | { type: "hotbar.swap"; payload: { from: number; to: number } }
  | { type: "hotbar.clear"; payload: { slot: number } };

// ビルドメニュー→枠はassign、枠→枠はswap、枠→枠外はclear。それ以外の組は無効なdropとしてnull
// buildMenuEntry→slot is assign, slot→slot is swap, slot→outside is clear; any other pairing is an invalid drop (null)
export function resolveDropAction(source: DragEndpoint, target: DragEndpoint): HotbarDropAction | null {
  if (source.kind === "buildMenuEntry") {
    if (target.kind !== "hotbarSlot") return null;
    return { type: "hotbar.assign", payload: { slot: target.index, id: source.id } };
  }

  if (source.kind === "hotbarSlot") {
    if (target.kind === "hotbarSlot") {
      // 自分自身へのdropは状態変化が無いため無視する
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
