import { create } from "zustand";

export type GameNotification = {
  id: number;
  category: "achievement" | "operationDenied";
  messageId: string;
  messageParams: string[];
  itemId: number | null;
};

type NotificationState = {
  notifications: GameNotification[];
  addNotification: (n: Omit<GameNotification, "id">) => void;
  removeNotification: (id: number) => void;
};

let nextId = 1;
const DISPLAY_MS = 5000;

export const useNotificationStore = create<NotificationState>((set) => ({
  notifications: [],
  addNotification: (n) => {
    const id = nextId++;
    set((s) => ({ notifications: [...s.notifications, { ...n, id }] }));
    // 5秒で自動消滅
    // Auto-dismiss after 5s
    setTimeout(() => set((s) => ({ notifications: s.notifications.filter((x) => x.id !== id) })), DISPLAY_MS);
  },
  removeNotification: (id) => set((s) => ({ notifications: s.notifications.filter((x) => x.id !== id) })),
}));
