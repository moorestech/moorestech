import { describe, it, expect, vi, beforeEach } from "vitest";
import { useNotificationStore } from "./notificationStore";

describe("notificationStore", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    useNotificationStore.setState({ notifications: [] });
  });

  it("追加され5秒後に消える", () => {
    useNotificationStore.getState().addNotification({
      category: "achievement",
      messageId: "achievement.researchCompleted",
      messageParams: ["Iron"],
      itemId: null,
    });
    expect(useNotificationStore.getState().notifications).toHaveLength(1);
    vi.advanceTimersByTime(5000);
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
});
