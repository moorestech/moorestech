import { describe, it, expect, vi, beforeEach } from "vitest";
import { NOTIFICATION_REMOVAL_FALLBACK_MS, useNotificationStore } from "./notificationStore";

describe("notificationStore", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    useNotificationStore.setState({ notifications: [] });
  });

  it("退場アニメが発火しなくても保険タイマーで消える", () => {
    useNotificationStore.getState().addNotification({
      category: "achievement",
      messageId: "achievement.researchCompleted",
      messageParams: ["Iron"],
      itemId: null,
      count: 0,
    });
    expect(useNotificationStore.getState().notifications).toHaveLength(1);
    vi.advanceTimersByTime(NOTIFICATION_REMOVAL_FALLBACK_MS - 1);
    expect(useNotificationStore.getState().notifications).toHaveLength(1);
    vi.advanceTimersByTime(1);
    expect(useNotificationStore.getState().notifications).toHaveLength(0);
  });

  it("itemIdを保持する", () => {
    useNotificationStore.getState().addNotification({
      category: "operationDenied",
      messageId: "denied.craftMaterialShortage",
      messageParams: [],
      itemId: 42,
      count: 0,
    });
    expect(useNotificationStore.getState().notifications[0].itemId).toBe(42);
  });

  it("退場用の状態を持たない", () => {
    useNotificationStore.getState().addNotification({
      category: "achievement",
      messageId: "achievement.unlockedItem",
      messageParams: [],
      itemId: null,
      count: 0,
    });
    expect(Object.keys(useNotificationStore.getState().notifications[0]).sort())
      .toEqual(["category", "count", "id", "itemId", "messageId", "messageParams"]);
  });

  it("表示中の同一アイテムの獲得は1行に加算される", () => {
    earnItem(5);
    earnItem(3);
    const notifications = useNotificationStore.getState().notifications;
    expect(notifications).toHaveLength(1);
    expect(notifications[0].count).toBe(8);
  });

  it("加算時はidが刷新され生存尺が回り直す", () => {
    earnItem(5);
    const firstId = useNotificationStore.getState().notifications[0].id;
    vi.advanceTimersByTime(NOTIFICATION_REMOVAL_FALLBACK_MS - 1);
    earnItem(3);
    expect(useNotificationStore.getState().notifications[0].id).not.toBe(firstId);

    // 旧idの保険タイマーが発火しても加算後の行を巻き添えにしない
    // The old id's fallback timer must not sweep away the merged row
    vi.advanceTimersByTime(1);
    expect(useNotificationStore.getState().notifications).toHaveLength(1);
    vi.advanceTimersByTime(NOTIFICATION_REMOVAL_FALLBACK_MS);
    expect(useNotificationStore.getState().notifications).toHaveLength(0);
  });

  it("別アイテムの獲得は別行になる", () => {
    earnItem(5, 7);
    earnItem(3, 9);
    expect(useNotificationStore.getState().notifications).toHaveLength(2);
  });

  it("獲得以外のカテゴリは同じ内容でも集約されない", () => {
    const unlocked = { category: "achievement" as const, messageId: "achievement.unlockedItem", messageParams: [], itemId: 7, count: 0 };
    useNotificationStore.getState().addNotification(unlocked);
    useNotificationStore.getState().addNotification(unlocked);
    expect(useNotificationStore.getState().notifications).toHaveLength(2);
  });
});

function earnItem(count: number, itemId = 7) {
  useNotificationStore.getState().addNotification({
    category: "itemEarned",
    messageId: "itemEarned.mined",
    messageParams: [],
    itemId,
    count,
  });
}
