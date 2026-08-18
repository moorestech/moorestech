import { create } from "zustand";
import type { NotificationData } from "@/bridge";

// 配信された通知(空スナップショットを除く)のカテゴリはbridgeの契約から導く。二重定義を避ける
// Categories come from the bridge contract's delivered variants (excluding the empty snapshot), avoiding a duplicate definition
type DeliveredNotificationData = Extract<NotificationData, { category: string }>;
type MessageCategory = Exclude<DeliveredNotificationData["category"], "itemEarned">;

// 獲得通知はアイコンと個数が必須、その他は個数を持たない。カテゴリで必要な項目が決まる
// Earned notifications require icon and amount; the others carry no amount. The category decides which fields exist
export type NewGameNotification =
  | { category: "itemEarned"; messageId: string; messageParams: string[]; itemId: number; count: number }
  | { category: MessageCategory; messageId: string; messageParams: string[]; itemId: number | null };

export type GameNotification = NewGameNotification & { id: number };

type NotificationState = {
  notifications: GameNotification[];
  addNotification: (n: NewGameNotification) => void;
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
      // 集約するのは獲得通知だけ。他カテゴリは同じ内容でも別行として積む
      // Only earned notifications aggregate; other categories stack as separate rows even when identical
      if (n.category !== "itemEarned") return { notifications: [...s.notifications, { ...n, id }] };

      // 表示中の同一アイテムの獲得行は作り直して数値を伸ばす。idを刷新すると再マウントされ入場アニメと生存尺が回り直す
      // A live earned row for the same item is rebuilt with a larger number; the renewed id remounts it so the enter animation and lifetime restart
      const merged = s.notifications.find(
        (x) => x.category === "itemEarned" && x.messageId === n.messageId && x.itemId === n.itemId,
      );
      const rest = merged ? s.notifications.filter((x) => x.id !== merged.id) : s.notifications;
      const count = (merged?.category === "itemEarned" ? merged.count : 0) + n.count;
      return { notifications: [...rest, { ...n, count, id }] };
    });
    // 加算でidが刷新された行を巻き添えにしないよう、対象idが残っていない時は何もしない
    // Do nothing when the target id is gone, so a row whose id was renewed by a merge is not swept away
    setTimeout(() => set((s) => (s.notifications.some((x) => x.id === id)
      ? { notifications: s.notifications.filter((x) => x.id !== id) }
      : s)), NOTIFICATION_REMOVAL_FALLBACK_MS);
  },
  removeNotification: (id) => set((s) => ({ notifications: s.notifications.filter((x) => x.id !== id) })),
}));
