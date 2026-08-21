// ホットバーD&Dの純粋ロジック
// Pure hotbar-D&D logic: resolves a drag source/target pair into the action to dispatch

// ホットバーの枠が持つのは配置対象(ブロック・列車車両・接続ツール・BP・BPコピー)だけで、持ち物のアイテムは入らない
// A hotbar slot only ever holds a placement target (block, train car, connect tool, blueprint, blueprint copy); inventory items never enter it
// 割当が生まれる唯一の経路はビルドメニューのエントリのドラッグで、持ち物からのドロップは仕様として存在しない
// The one and only path that creates an assignment is dragging a build-menu entry; dropping from the inventory does not exist by design
// ドラッグ元。枠外から掴むことはないのでoutsideを持たない
// A drag source; nothing is ever grabbed from outside a slot, so "outside" is not one of these
export type HotbarDragSource =
  | { kind: "buildMenuEntry"; id: string }
  | { kind: "hotbarSlot"; index: number };

// ドロップ先。ビルドメニューのエントリへは落とせない
// A drop target; a build-menu entry is never a place to drop onto
export type HotbarDropTarget =
  | { kind: "hotbarSlot"; index: number }
  | { kind: "outside" };

type HotbarDropAction =
  | { type: "hotbar.assign"; payload: { slot: number; id: string } }
  | { type: "hotbar.swap"; payload: { from: number; to: number } }
  | { type: "hotbar.clear"; payload: { slot: number } };

// 枠の組み合わせをassign/swap/clearへ
// buildMenuEntry→slot is assign, slot→slot is swap, slot→outside is clear
export function resolveDropAction(source: HotbarDragSource, target: HotbarDropTarget): HotbarDropAction | null {
  switch (source.kind) {
    case "buildMenuEntry":
      // メニューのエントリは枠へ落としたときだけ割当になる
      // A menu entry only assigns when it lands on a slot
      return target.kind === "hotbarSlot" ? { type: "hotbar.assign", payload: { slot: target.index, id: source.id } } : null;
    case "hotbarSlot":
      return resolveFromSlot(source.index, target);
    default:
      return assertNever(source);
  }
}

function resolveFromSlot(sourceIndex: number, target: HotbarDropTarget): HotbarDropAction | null {
  switch (target.kind) {
    case "hotbarSlot":
      // 自分自身へのdropは何も変えない
      // A drop onto itself changes nothing
      return target.index === sourceIndex ? null : { type: "hotbar.swap", payload: { from: sourceIndex, to: target.index } };
    case "outside":
      return { type: "hotbar.clear", payload: { slot: sourceIndex } };
    default:
      return assertNever(target);
  }
}

// 種別を足したら網羅漏れをコンパイル時に落とす
// Adding a kind breaks compilation here instead of silently falling through
function assertNever(value: never): never {
  throw new Error(`Unhandled hotbar D&D endpoint: ${JSON.stringify(value)}`);
}
