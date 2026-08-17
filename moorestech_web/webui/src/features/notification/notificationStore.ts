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
// 生存尺は7秒。出入りアニメの尺はこの内側に含める（CSSへはHostが変数で渡す）
// The lifetime is 7s and contains the enter/exit animation; the host passes it to CSS as a variable
export const NOTIFICATION_DISPLAY_MS = 7000;

export const useNotificationStore = create<NotificationState>((set) => ({
  notifications: [],
  addNotification: (n) => {
    const id = nextId++;
    set((s) => ({ notifications: [...s.notifications, { ...n, id }] }));
    // 生存尺の経過で削除する。退場アニメはこの尺から逆算してCSS側が描く
    // Remove it when the lifetime elapses; CSS derives the exit animation from that same lifetime
    setTimeout(() => set((s) => ({ notifications: s.notifications.filter((x) => x.id !== id) })), NOTIFICATION_DISPLAY_MS);
  },
  removeNotification: (id) => set((s) => ({ notifications: s.notifications.filter((x) => x.id !== id) })),
}));
