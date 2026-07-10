import { describe, expect, it } from "vitest";
import { selectPayload, deletePayload } from "./buildMenuLogic";

describe("buildMenuLogic", () => {
  it("selectPayload はエントリの種別とキーを写す", () => {
    const entry = { entryType: "blueprint" as const, entryKey: "家", label: "家", tooltip: "家" };
    expect(selectPayload(entry)).toEqual({ entryType: "blueprint", entryKey: "家" });
  });
  it("deletePayload はBP名を写す", () => {
    expect(deletePayload("家")).toEqual({ name: "家" });
  });
});
