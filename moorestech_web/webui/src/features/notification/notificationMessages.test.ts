import { describe, it, expect } from "vitest";
import { resolveNotificationTemplate, buildInterpolationValues } from "./notificationMessages";

describe("notificationMessages", () => {
  it("既知のmessageIdはテンプレートを返す", () => {
    expect(resolveNotificationTemplate("denied.craftMaterialShortage")).toContain("materials");
  });
  it("未知のmessageIdはID文字列をそのまま返す", () => {
    expect(resolveNotificationTemplate("unknown.id")).toBe("unknown.id");
  });
  it("paramsをp0,p1に変換する", () => {
    expect(buildInterpolationValues(["a", "b"])).toEqual({ p0: "a", p1: "b" });
  });
});
