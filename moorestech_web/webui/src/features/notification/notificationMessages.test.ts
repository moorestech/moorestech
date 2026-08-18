import { describe, it, expect } from "vitest";
import { L } from "@/shared/i18n";
import { resolveNotificationKey, resolveNotificationParams, buildInterpolationValues } from "./notificationMessages";

describe("notificationMessages", () => {
  it("既知のmessageIdは型付きキーを返す", () => {
    expect(resolveNotificationKey("denied.craftMaterialShortage")).toBe(
      L.ui.notification.craftMaterialShortage,
    );
  });
  it("未知のmessageIdは専用キーで可視化する", () => {
    expect(resolveNotificationKey("unknown.id")).toBe(L.ui.notification.unknownMessage);
  });
  it("獲得通知は専用キーを返す", () => {
    expect(resolveNotificationKey("itemEarned.mined")).toBe(L.ui.notification.itemEarned);
  });
  it("messageIdとparamsとcountを補間値へ変換する", () => {
    expect(buildInterpolationValues("known.id", ["a", "b"], 0)).toEqual({
      messageId: "known.id",
      count: 0,
      p0: "a",
      p1: "b",
    });
    expect(buildInterpolationValues("itemEarned.mined", [], 8)).toEqual({
      messageId: "itemEarned.mined",
      count: 8,
    });
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
