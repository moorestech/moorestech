import { describe, expect, it } from "vitest";
import { NotificationDataSchema } from "./ui";

describe("NotificationDataSchema", () => {
  it("itemId: nullをomittedと同様に受理する（シリアライザ揺れ耐性）", () => {
    const parsed = NotificationDataSchema.parse({
      seq: 1, category: "achievement", messageId: "achievement.researchCompleted", messageParams: [], itemId: null,
    });
    expect(parsed.itemId ?? null).toBeNull();
  });
});
