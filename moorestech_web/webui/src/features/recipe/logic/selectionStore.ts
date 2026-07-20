import { create } from "zustand";

// レシピビューアのアイテム選択状態（recipe feature ローカル。remote topic データは入れない）
// Item-selection state local to the recipe feature (never holds remote topic data)
type ItemSelectionState = {
  selectedItemId: number | null;
  setSelectedItem: (itemId: number) => void;
  clearSelectedItem: () => void;
};

export const useItemSelectionStore = create<ItemSelectionState>((set) => ({
  selectedItemId: null,
  setSelectedItem: (itemId) => set({ selectedItemId: itemId }),
  clearSelectedItem: () => set({ selectedItemId: null }),
}));

// フック外（App の Esc ハンドラ等）から選択を解除する命令的アクセサ
// Imperative accessor to clear the selection outside hooks (e.g. the App Esc handler)
export function clearSelectedItem(): void {
  useItemSelectionStore.getState().clearSelectedItem();
}
