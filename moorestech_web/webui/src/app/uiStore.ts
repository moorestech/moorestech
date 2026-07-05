import { create } from "zustand";

// UI ローカル状態のみ（remote topic データは入れない）。grab は WS topic 由来なので絶対に入れない。
// UI-local state only (no remote topic data here). grab comes from a WS topic and must never live here.
type UiState = {
  selectedItemId: number | null;
  setSelectedItem: (itemId: number) => void;
  clearSelectedItem: () => void;
};

export const useUiStore = create<UiState>((set) => ({
  selectedItemId: null,
  setSelectedItem: (itemId) => set({ selectedItemId: itemId }),
  clearSelectedItem: () => set({ selectedItemId: null }),
}));
