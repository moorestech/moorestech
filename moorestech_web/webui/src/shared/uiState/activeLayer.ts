import { readTopic, Topics, UiStateNames } from "@/bridge";

// 前面UI層の優先順位
// Frontmost UI layer priority
export type ActiveLayer = "modal" | "blockInventory" | "research" | "buildMenu" | "game";

// 各オーバーレイの有無から最前面レイヤーを導出する純関数（優先順位を1箇所に固定）
// Pure derivation of the frontmost layer from overlay presence (priority fixed in one place)
export function deriveActiveLayer(input: { modalOpen: boolean; blockInventoryOpen: boolean; researchOpen: boolean; buildMenuOpen: boolean }): ActiveLayer {
  if (input.modalOpen) return "modal";
  if (input.blockInventoryOpen) return "blockInventory";
  if (input.researchOpen) return "research";
  if (input.buildMenuOpen) return "buildMenu";
  return "game";
}

// topicStore の現在値からレイヤーを読むセレクタ。ゲーム系入力の排他判定に使う
// Selector reading the layer from the current topicStore values; used to gate game-layer inputs
export function readActiveLayer(): ActiveLayer {
  const modal = readTopic(Topics.modal);
  const block = readTopic(Topics.blockInventory);
  const uiState = readTopic(Topics.uiState);
  return deriveActiveLayer({
    modalOpen: modal?.modal != null,
    blockInventoryOpen: block?.open === true,
    researchOpen: uiState?.state === UiStateNames.researchTree,
    buildMenuOpen: uiState?.state === UiStateNames.buildMenu,
  });
}
