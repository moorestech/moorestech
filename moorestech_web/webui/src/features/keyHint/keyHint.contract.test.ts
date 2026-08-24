import { describe, expect, it } from "vitest";
import { UiStateDataSchema } from "@/bridge/contract/schemas/ui";

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
