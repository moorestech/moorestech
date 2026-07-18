import { useGameLayerKeydown } from "@/shared/uiState";
import { clearSelectedItem } from "../selectionStore";

// ゲームレイヤーのEscでレシピ選択を解除する
// Clear recipe selection on Escape in the game layer
export default function RecipeSelectionKeyHandler() {
  useGameLayerKeydown((event) => {
    if (event.key !== "Escape") return;
    clearSelectedItem();
  });
  return null;
}
