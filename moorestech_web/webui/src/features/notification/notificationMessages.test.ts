import { describe, it, expect, vi } from "vitest";
import { L } from "@/shared/i18n";
import { resolveNotificationKey, resolveNotificationParams, buildInterpolationValues, resolveNotificationText } from "./notificationMessages";

describe("notificationMessages", () => {
  it("既知のmessageIdは型付きキーを返す", () => {
    expect(resolveNotificationKey("denied.craftMaterialShortage")).toBe(
      L.ui.notification.craftMaterialShortage,
    );
  });
  it("未知のmessageIdは専用キーで可視化する", () => {
    expect(resolveNotificationKey("unknown.id")).toBe(L.ui.notification.unknownMessage);
  });
  it("獲得通知のmessageIdは表から到達できない", () => {
    expect(resolveNotificationKey("itemEarned.mined")).toBe(L.ui.notification.unknownMessage);
  });
  it("messageIdとparamsを補間値へ変換する", () => {
    expect(buildInterpolationValues("known.id", ["a", "b"])).toEqual({
      messageId: "known.id",
      p0: "a",
      p1: "b",
    });
  });
  it("アイテム名とcountを補間するのは獲得通知だけ", () => {
    const translate = (key: string) => `resolved:${key}`;
    const resolveItemDisplayName = (itemId: number) => `item:${itemId}`;
    expect(resolveNotificationText(
      { category: "itemEarned", messageId: "itemEarned.mined", messageParams: [], itemId: 7, count: 8, id: 1, lifetimeEpoch: 0 },
      translate,
      resolveItemDisplayName,
    )).toEqual({ key: L.ui.notification.itemEarned, values: { messageId: "itemEarned.mined", itemName: "item:7", count: 8 } });

    expect(resolveNotificationText(
      { category: "operationDenied", messageId: "denied.craftResultFull", messageParams: ["a"], itemId: null, id: 2, lifetimeEpoch: 0 },
      translate,
      resolveItemDisplayName,
    )).toEqual({ key: L.ui.notification.craftResultFull, values: { messageId: "denied.craftResultFull", p0: "a" } });
  });

  it("獲得通知はmessageIdに依らず獲得テンプレートを使う", () => {
    const translate = (key: string) => `resolved:${key}`;
    expect(resolveNotificationText(
      { category: "itemEarned", messageId: "itemEarned.drifted", messageParams: [], itemId: 7, count: 3, id: 1, lifetimeEpoch: 0 },
      translate,
      (itemId: number) => `item:${itemId}`,
    )).toEqual({ key: L.ui.notification.itemEarned, values: { messageId: "itemEarned.drifted", itemName: "item:7", count: 3 } });
  });

  it("獲得messageIdがmessage系categoryで来たら未知通知へ落とす", () => {
    const translate = (key: string) => `resolved:${key}`;
    const { key, values } = resolveNotificationText(
      { category: "achievement", messageId: "itemEarned.mined", messageParams: [], itemId: null, id: 2, lifetimeEpoch: 0 },
      translate,
      (itemId: number) => `item:${itemId}`,
    );
    expect(key).toBe(L.ui.notification.unknownMessage);
    expect(values).not.toHaveProperty("itemName");
  });

  it("獲得通知以外はアイテム名を解決しない", () => {
    const translate = (key: string) => `resolved:${key}`;
    const resolveItemDisplayName = vi.fn(() => "should-not-be-called");
    resolveNotificationText(
      { category: "achievement", messageId: "achievement.unlockedItem", messageParams: [], itemId: 7, id: 3, lifetimeEpoch: 0 },
      translate,
      resolveItemDisplayName,
    );
    expect(resolveItemDisplayName).not.toHaveBeenCalled();
  });
  it("Guidパラメータ通知はcontentキーで表示名へ解決する", () => {
    const guid = "13C3D42F-BBBC-5EB4-8CD0-7B841EF53079";
    const translate = (key: string) => `resolved:${key}`;
    expect(resolveNotificationParams("achievement.challengeCompleted", [guid], translate)).toEqual([
      `resolved:challenge.${guid.toLowerCase()}.title`,
    ]);
    expect(resolveNotificationParams("achievement.researchCompleted", [guid], translate)).toEqual([
      `resolved:research.${guid.toLowerCase()}.name`,
    ]);
  });
  it("Guidパラメータを持たない通知はparamsを素通しする", () => {
    const translate = () => "should-not-be-called";
    expect(resolveNotificationParams("denied.craftResultFull", ["raw"], translate)).toEqual(["raw"]);
  });
});
