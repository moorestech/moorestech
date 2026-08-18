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
      .toEqual(["category", "id", "itemId", "messageId", "messageParams"]);
  });
});
