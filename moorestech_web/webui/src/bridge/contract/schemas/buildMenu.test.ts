import { describe, expect, it } from "vitest";
import { BuildMenuEntryDataSchema } from "./buildMenu";

describe("BuildMenuEntryDataSchema", () => {
  it("id+kind契約をパースできる", () => {
    const entry = BuildMenuEntryDataSchema.parse({
      id: "3f8f6de0-0000-4000-8000-000000000001",
      kind: "blueprintCopy",
      label: "ブループリントコピー",
      category: "ツール",
      subCategory: "ツール",
      requiredItems: [],
    });
    expect(entry.id).toBe("3f8f6de0-0000-4000-8000-000000000001");
  });

  it("旧entryType/entryKey契約は拒否する", () => {
    expect(() =>
      BuildMenuEntryDataSchema.parse({
        entryType: "block",
        entryKey: "1",
        label: "x",
        category: "c",
        subCategory: "s",
        requiredItems: [],
      })
    ).toThrow();
  });
});
