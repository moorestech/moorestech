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
// 生存尺は7秒、出入りを含む
// 7s lifetime contains enter/exit
export const NOTIFICATION_DISPLAY_MS = 7000;

export const useNotificationStore = create<NotificationState>((set) => ({
  notifications: [],
  addNotification: (n) => {
    const id = nextId++;
    set((s) => ({ notifications: [...s.notifications, { ...n, id }] }));
    // 生存尺経過で削除、退場と連動
    // Removed when the lifetime elapses, in sync with exit
    setTimeout(() => set((s) => ({ notifications: s.notifications.filter((x) => x.id !== id) })), NOTIFICATION_DISPLAY_MS);
  },
  removeNotification: (id) => set((s) => ({ notifications: s.notifications.filter((x) => x.id !== id) })),
}));
