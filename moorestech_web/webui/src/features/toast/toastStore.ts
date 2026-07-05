import { create } from "zustand";
import type { NotifyVariant } from "@/bridge/notify";

// variant は bridge の通知契約と共有する（error=赤 / info=青の色分けに使う）
// The variant is shared with the bridge notify contract (drives error=red / info=blue coloring)
export type ToastVariant = NotifyVariant;
export type Toast = { id: number; message: string; variant: ToastVariant };

type ToastState = {
  toasts: Toast[];
  addToast: (message: string, variant: ToastVariant) => void;
  removeToast: (id: number) => void;
};

let nextId = 1;

export const useToastStore = create<ToastState>((set) => ({
  toasts: [],
  addToast: (message, variant) => {
    const id = nextId++;
    set((s) => ({ toasts: [...s.toasts, { id, message, variant }] }));
    // 3秒で自動消滅（既存 ToastHost の挙動を保全）
    // Auto-dismiss after 3s (preserves the existing ToastHost behavior)
    setTimeout(() => set((s) => ({ toasts: s.toasts.filter((t) => t.id !== id) })), 3000);
  },
  removeToast: (id) => set((s) => ({ toasts: s.toasts.filter((t) => t.id !== id) })),
}));

// React 外（bridge）からの emit を getState 経由で保全
// Preserve emits from outside React (bridge) via getState
export function emitToast(message: string, variant: ToastVariant) {
  useToastStore.getState().addToast(message, variant);
}
