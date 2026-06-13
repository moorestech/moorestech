import { create } from "zustand";

export type Toast = { id: number; message: string };

type ToastState = {
  toasts: Toast[];
  addToast: (message: string) => void;
  removeToast: (id: number) => void;
};

let nextId = 1;

export const useToastStore = create<ToastState>((set) => ({
  toasts: [],
  addToast: (message) => {
    const id = nextId++;
    set((s) => ({ toasts: [...s.toasts, { id, message }] }));
    // 3秒で自動消滅（既存 ToastHost の挙動を保全）
    // Auto-dismiss after 3s (preserves the existing ToastHost behavior)
    setTimeout(() => set((s) => ({ toasts: s.toasts.filter((t) => t.id !== id) })), 3000);
  },
  removeToast: (id) => set((s) => ({ toasts: s.toasts.filter((t) => t.id !== id) })),
}));

// React 外（bridge）からの emit を getState 経由で保全
// Preserve emits from outside React (bridge) via getState
export function emitToast(message: string) {
  useToastStore.getState().addToast(message);
}
