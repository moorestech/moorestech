import { create } from "zustand";
import type { NotificationData } from "@/bridge";

// categoryはbridgeから導く
// Categories come from the bridge contract, avoiding duplication
type DeliveredNotificationData = Extract<NotificationData, { category: string }>;
type MessageCategory = Exclude<DeliveredNotificationData["category"], "itemEarned">;

// 必要な項目はカテゴリで決まる
// The category decides which fields exist
export type NewGameNotification =
  | { category: "itemEarned"; messageId: string; messageParams: string[]; itemId: number; count: number }
  | { category: MessageCategory; messageId: string; messageParams: string[]; itemId: number | null };

export type GameNotification = NewGameNotification & { id: number; lifetimeEpoch: number };

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

export const useNotificationStore = create<NotificationState>((set, get) => ({
  notifications: [],
  addNotification: (n) => {
    // 集約するのは獲得通知だけ。合流先は同じアイテム・同じmessageIdの表示中の行
    // Only earned notifications aggregate; the merge target is a visible row with the same item and messageId
    const merged = n.category === "itemEarned" ? findEarnedRow(get().notifications, n.messageId, n.itemId) : null;

    if (n.category === "itemEarned" && merged) {
      // idは据え置きlifetimeEpochだけ進める。行のDOMとアイコンと表示位置が保たれる
      // The id stays and only lifetimeEpoch advances, preserving the row's DOM, icon, and position
      const row: GameNotification = { ...n, count: merged.count + n.count, id: merged.id, lifetimeEpoch: merged.lifetimeEpoch + 1 };
      set((s) => ({ notifications: s.notifications.map((x) => (x.id === row.id ? row : x)) }));
      scheduleRemovalFallback(row.id, row.lifetimeEpoch);
      return;
    }

    const row: GameNotification = { ...n, id: nextId++, lifetimeEpoch: 0 };
    set((s) => ({ notifications: [...s.notifications, row] }));
    scheduleRemovalFallback(row.id, row.lifetimeEpoch);
  },
  removeNotification: (id) => set((s) => ({ notifications: s.notifications.filter((x) => x.id !== id) })),
}));

// countを読むため獲得通知へ絞り込んだ行を返す
// Returns the row narrowed to an earned notification so its count is readable
function findEarnedRow(notifications: GameNotification[], messageId: string, itemId: number) {
  const found = notifications.find((x) => x.category === "itemEarned" && x.messageId === messageId && x.itemId === itemId);
  return found?.category === "itemEarned" ? found : null;
}

// 生存尺が回り直した行は別epochになるので、古いタイマーでは消えない
// A row whose lifetime restarted carries a new epoch, so the stale timer no longer removes it
function scheduleRemovalFallback(id: number, lifetimeEpoch: number) {
  setTimeout(() => useNotificationStore.setState((s) => (
    s.notifications.some((x) => x.id === id && x.lifetimeEpoch === lifetimeEpoch)
      ? { notifications: s.notifications.filter((x) => x.id !== id) }
      : s)), NOTIFICATION_REMOVAL_FALLBACK_MS);
}
