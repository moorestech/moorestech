import { assert, describe, expect, it } from "vitest";
import { BuildMenuEntryDataSchema } from "./buildMenu";

describe("BuildMenuEntryDataSchema", () => {
  it("id+kind契約をパースできる", () => {
    const entry = BuildMenuEntryDataSchema.parse({
      id: "3f8f6de0-0000-4000-8000-000000000001",
      kind: "blueprintCopy",
      categoryGuid: "10000000-0000-4000-8000-000000000001",
      subCategoryGuid: "20000000-0000-4000-8000-000000000001",
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
    }).label).toBe("starter-base");
  });

  it("blockはsetPlacementを任意で受理し、perCostが1以下なら弾く", () => {
    const blockEntryBase = {
      id: "30000000-0000-4000-8000-000000000001",
      kind: "block" as const,
      categoryGuid: "10000000-0000-4000-8000-000000000001",
      subCategoryGuid: "20000000-0000-4000-8000-000000000001",
      requiredItems: [{ itemId: 3, count: 1 }],
    };

    const entry = BuildMenuEntryDataSchema.parse({ ...blockEntryBase, setPlacement: { perCost: 3, remaining: 2 } });
    assert(entry.kind === "block");
    expect(entry.setPlacement).toEqual({ perCost: 3, remaining: 2 });

    // 財布を使わないブロックはキーごと省略されて届く
    // Blocks that bypass the wallet arrive with the key omitted entirely
    const walletlessEntry = BuildMenuEntryDataSchema.parse(blockEntryBase);
    assert(walletlessEntry.kind === "block");
    expect(walletlessEntry.setPlacement).toBeUndefined();

    expect(() => BuildMenuEntryDataSchema.parse({ ...blockEntryBase, setPlacement: { perCost: 1, remaining: 0 } })).toThrow();
  });

  it("block以外へsetPlacementを載せたpayloadは拒否する", () => {
    expect(() => BuildMenuEntryDataSchema.parse({
      id: "8f9c2a51-0000-4000-8000-000000000001",
      kind: "trainCar",
      categoryGuid: "10000000-0000-4000-8000-000000000001",
      subCategoryGuid: "20000000-0000-4000-8000-000000000001",
      requiredItems: [],
      setPlacement: { perCost: 3, remaining: 2 },
    })).toThrow();
  });
});
