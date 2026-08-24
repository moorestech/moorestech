import { describe, expect, it } from "vitest";
import { NotificationDataSchema, UiStateDataSchema } from "./ui";

describe("NotificationDataSchema", () => {
  it("itemId: nullをomittedと同様に受理する（シリアライザ揺れ耐性）", () => {
    const parsed = NotificationDataSchema.parse({
      seq: 1, category: "achievement", messageId: "achievement.researchCompleted", messageParams: [], itemId: null,
    });
    expect("itemId" in parsed ? parsed.itemId ?? null : null).toBeNull();
  });

  it("snapshotの空オブジェクトを受理する", () => {
    expect(NotificationDataSchema.safeParse({}).success).toBe(true);
  });

  it("itemEarnedはitemIdとcountの欠損を弾く", () => {
    const base = { seq: 1, category: "itemEarned", messageId: "itemEarned.mined", messageParams: [] };
    expect(NotificationDataSchema.safeParse({ ...base, itemId: 5, count: 8 }).success).toBe(true);
    expect(NotificationDataSchema.safeParse({ ...base, itemId: 5 }).success).toBe(false);
    expect(NotificationDataSchema.safeParse({ ...base, count: 8 }).success).toBe(false);
    expect(NotificationDataSchema.safeParse({ ...base, itemId: 5, count: 0 }).success).toBe(false);
  });
});

// ヒント配列はC#のUIStateが宣言した内容をそのまま運ぶ（ADR-0032）
// The hint array carries exactly what C#'s UIState declared (ADR-0032)
describe("UiStateDataSchema", () => {
  it("keyHintsを受理する", () => {
    const parsed = UiStateDataSchema.safeParse({
      state: "GameScreen",
      keyHints: [{ keyNameKey: "ui.keyHint.key.tab", textKey: "ui.keyHint.text.inventory" }],
    });
    expect(parsed.success).toBe(true);
  });

  it("keyHints未着のペイロードも受理する", () => {
    const parsed = UiStateDataSchema.safeParse({ state: "GameScreen" });
    expect(parsed.success).toBe(true);
    expect(parsed.success && parsed.data.keyHints).toEqual([]);
  });
});
