import { describe, it, expect } from "vitest";
import { L } from "@/shared/i18n";
import { resolveNotificationKey, buildInterpolationValues } from "./notificationMessages";

describe("notificationMessages", () => {
  it("既知のmessageIdは型付きキーを返す", () => {
    expect(resolveNotificationKey("denied.craftMaterialShortage")).toBe(
      L.ui.notification.craftMaterialShortage,
    );
  });
  it("未知のmessageIdは専用キーで可視化する", () => {
    expect(resolveNotificationKey("unknown.id")).toBe(L.ui.notification.unknownMessage);
  });
  it("messageIdとparamsを補間値へ変換する", () => {
    expect(buildInterpolationValues("known.id", ["a", "b"])).toEqual({
      messageId: "known.id",
      p0: "a",
      p1: "b",
    });
  });
});
