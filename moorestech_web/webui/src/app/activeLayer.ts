import { readTopic, Topics } from "@/bridge";

// 最前面 UI レイヤー。modal 最優先 → block インベントリ → 素の game
// Frontmost UI layer: modal first → block inventory → bare game
export type ActiveLayer = "modal" | "blockInventory" | "game";

// 各オーバーレイの有無から最前面レイヤーを導出する純関数（優先順位を1箇所に固定）
// Pure derivation of the frontmost layer from overlay presence (priority fixed in one place)
export function deriveActiveLayer(input: { modalOpen: boolean; blockInventoryOpen: boolean }): ActiveLayer {
  if (input.modalOpen) return "modal";
  if (input.blockInventoryOpen) return "blockInventory";
  return "game";
}

// topicStore の現在値からレイヤーを読むセレクタ。ゲーム系入力の排他判定に使う
// Selector reading the layer from the current topicStore values; used to gate game-layer inputs
export function readActiveLayer(): ActiveLayer {
  const modal = readTopic(Topics.modal);
  const block = readTopic(Topics.blockInventory);
  return deriveActiveLayer({
    modalOpen: modal?.modal != null,
    blockInventoryOpen: block?.open === true,
  });
}
