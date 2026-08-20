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
    });
    expect(useNotificationStore.getState().notifications[0].itemId).toBe(42);
  });

  it("退場用の状態を持たない", () => {
    useNotificationStore.getState().addNotification({
      category: "achievement",
      messageId: "achievement.unlockedItem",
      messageParams: [],
      itemId: null,
    });
    expect(Object.keys(useNotificationStore.getState().notifications[0]).sort())
      .toEqual(["category", "id", "itemId", "lifetimeEpoch", "messageId", "messageParams"]);
  });

  it("表示中の同一アイテムの獲得は1行に加算される", () => {
    earnItem(5, 7, "itemEarned.mined");
    earnItem(3, 7, "itemEarned.mined");
    const notifications = useNotificationStore.getState().notifications;
    expect(notifications).toHaveLength(1);
    expect(earnedCountOf(0)).toBe(8);
  });

  it("加算時はidを据え置きlifetimeEpochで生存尺が回り直す", () => {
    earnItem(5, 7, "itemEarned.mined");
    const firstId = useNotificationStore.getState().notifications[0].id;
    vi.advanceTimersByTime(NOTIFICATION_REMOVAL_FALLBACK_MS - 1);
    earnItem(3, 7, "itemEarned.mined");
    expect(useNotificationStore.getState().notifications[0].id).toBe(firstId);
    expect(useNotificationStore.getState().notifications[0].lifetimeEpoch).toBe(1);

    // 旧epochのタイマーで加算後の行を消さない
    // The stale epoch's timer must not remove the merged row
    vi.advanceTimersByTime(1);
    expect(useNotificationStore.getState().notifications).toHaveLength(1);
    vi.advanceTimersByTime(NOTIFICATION_REMOVAL_FALLBACK_MS);
    expect(useNotificationStore.getState().notifications).toHaveLength(0);
  });

  it("加算しても表示順は入れ替わらない", () => {
    earnItem(5, 7, "itemEarned.mined");
    earnItem(1, 9, "itemEarned.mined");
    earnItem(3, 7, "itemEarned.mined");

    const notifications = useNotificationStore.getState().notifications;
    expect(notifications.map((x) => (x.category === "itemEarned" ? x.itemId : null))).toEqual([7, 9]);
    expect(earnedCountOf(0)).toBe(8);
  });

  it("別アイテムの獲得は別行になる", () => {
    earnItem(5, 7, "itemEarned.mined");
    earnItem(3, 9, "itemEarned.mined");
    expect(useNotificationStore.getState().notifications).toHaveLength(2);
  });

  it("messageIdが異なる同一アイテムの獲得は合流しない", () => {
    earnItem(5, 7, "itemEarned.mined");
    earnItem(3, 7, "itemEarned.crafted");
    expect(useNotificationStore.getState().notifications).toHaveLength(2);
  });

  it("獲得以外のカテゴリは同じ内容でも集約されない", () => {
    const unlocked = { category: "achievement" as const, messageId: "achievement.unlockedItem", messageParams: [], itemId: 7 };
    useNotificationStore.getState().addNotification(unlocked);
    useNotificationStore.getState().addNotification(unlocked);
    expect(useNotificationStore.getState().notifications).toHaveLength(2);
  });
});

// countはitemEarnedへ絞り込んで読む
// count is read after narrowing to itemEarned
function earnedCountOf(index: number) {
  const notification = useNotificationStore.getState().notifications[index];
  return notification.category === "itemEarned" ? notification.count : null;
}

function earnItem(count: number, itemId: number, messageId: string) {
  useNotificationStore.getState().addNotification({
    category: "itemEarned",
    messageId,
    messageParams: [],
    itemId,
    count,
  });
}
