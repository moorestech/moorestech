import { create } from "zustand";

export type GameNotification = {
  id: number;
  category: "achievement" | "operationDenied" | "itemEarned";
  messageId: string;
  messageParams: string[];
  itemId: number | null;
  count: number;
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
// 除去の主担当は退場アニメの終了。タブ非表示等でアニメが発火しない場合だけこの保険が回収する
// The exit animation's end drives removal; this fallback only collects rows whose animation never fired (hidden tab, etc.)
export const NOTIFICATION_REMOVAL_FALLBACK_MS = NOTIFICATION_DISPLAY_MS + 1000;

export const useNotificationStore = create<NotificationState>((set) => ({
  notifications: [],
  addNotification: (n) => {
    const id = nextId++;
    set((s) => {
      // 表示中の同一アイテムの獲得行は作り直して数値を伸ばす。idを刷新すると再マウントされ入場アニメと生存尺が回り直す
      // A live earned row for the same item is rebuilt with a larger number; the renewed id remounts it so the enter animation and lifetime restart
      const merged = n.category === "itemEarned"
        ? s.notifications.find((x) => x.category === "itemEarned" && x.itemId === n.itemId)
        : undefined;
      const rest = merged ? s.notifications.filter((x) => x.id !== merged.id) : s.notifications;
      const count = (merged?.count ?? 0) + n.count;
      return { notifications: [...rest, { ...n, count, id }] };
    });
    setTimeout(() => set((s) => ({ notifications: s.notifications.filter((x) => x.id !== id) })), NOTIFICATION_REMOVAL_FALLBACK_MS);
  },
  removeNotification: (id) => set((s) => ({ notifications: s.notifications.filter((x) => x.id !== id) })),
}));
