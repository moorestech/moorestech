import { describe, expect, it } from "vitest";
import { BuildMenuEntryDataSchema } from "./buildMenu";

describe("BuildMenuEntryDataSchema", () => {
  it("id+kind契約をパースできる", () => {
    const entry = BuildMenuEntryDataSchema.parse({
      id: "3f8f6de0-0000-4000-8000-000000000001",
      kind: "blueprintCopy",
      categoryGuid: "10000000-0000-4000-8000-000000000001",
      subCategoryGuid: "20000000-0000-4000-8000-000000000001",
      requiredItems: [],
      placementsPerCost: 1,
      remainingPlacementCount: 0,
    });
    expect(entry.id).toBe("3f8f6de0-0000-4000-8000-000000000001");
  });

  it("旧entryType/entryKey契約は拒否する", () => {
    expect(() =>
      BuildMenuEntryDataSchema.parse({
        entryType: "block",
        entryKey: "1",
        label: "x",
        categoryGuid: "10000000-0000-4000-8000-000000000001",
        subCategoryGuid: "20000000-0000-4000-8000-000000000001",
        requiredItems: [],
      })
    ).toThrow();
  });

  it("新契約へ旧identityを混在させたpayloadは拒否する", () => {
    expect(() => BuildMenuEntryDataSchema.parse({
      id: "30000000-0000-4000-8000-000000000001",
      kind: "block",
      entryType: "block",
      entryKey: "1",
      categoryGuid: "10000000-0000-4000-8000-000000000001",
      subCategoryGuid: "20000000-0000-4000-8000-000000000001",
      requiredItems: [],
    })).toThrow();
  });

  it("マスタ由来エントリのraw labelを拒否する", () => {
    expect(() => BuildMenuEntryDataSchema.parse({
      id: "30000000-0000-4000-8000-000000000001",
      kind: "block",
      label: "鉄の機械",
      categoryGuid: "10000000-0000-4000-8000-000000000001",
      subCategoryGuid: "20000000-0000-4000-8000-000000000001",
      requiredItems: [],
    })).toThrow();
  });

  it("ユーザー命名blueprintだけlabelを受理する", () => {
    expect(BuildMenuEntryDataSchema.parse({
      id: "60000000-0000-4000-8000-000000000001",
      kind: "blueprint",
      label: "starter-base",
      categoryGuid: "10000000-0000-4000-8000-000000000001",
      subCategoryGuid: "20000000-0000-4000-8000-000000000001",
      requiredItems: [],
      placementsPerCost: 1,
      remainingPlacementCount: 0,
    }).label).toBe("starter-base");
  });

  it("placementsPerCost と remainingPlacementCount を必須で受理する", () => {
    const entry = BuildMenuEntryDataSchema.parse({
      id: "30000000-0000-4000-8000-000000000001",
      kind: "block",
      categoryGuid: "10000000-0000-4000-8000-000000000001",
      subCategoryGuid: "20000000-0000-4000-8000-000000000001",
      requiredItems: [{ itemId: 3, count: 1 }],
      placementsPerCost: 3,
      remainingPlacementCount: 2,
    });
    expect(entry.placementsPerCost).toBe(3);
    expect(() => BuildMenuEntryDataSchema.parse({ ...entry, placementsPerCost: 0 })).toThrow();
  });
});
