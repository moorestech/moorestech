import { describe, it, expect } from "vitest";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

import {
  TutorialAnchorIds,
  TutorialAnchorDynamicPrefixes,
  buildMenuEntryAnchorId,
  equipmentSlotAnchorId,
  inventoryItemAnchorId,
  recipeItemAnchorId,
} from "./anchorIds";

// Unity側TutorialAnchorIdMapperの照合テストと同一フィクスチャを参照し、乖離を検知する
// Shared with Unity's TutorialAnchorIdMapper contract test to catch drift between the two sides
const fixturePath = fileURLToPath(
  new URL(
    "../../../../../moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures/tutorial_anchor_ids.json",
    import.meta.url,
  ),
);

function loadFixture(): { staticIds: string[]; dynamicPrefixes: Record<string, string> } {
  return JSON.parse(readFileSync(fixturePath, "utf8"));
}

describe("tutorial anchor IDs (shared fixture with Unity)", () => {
  it("static IDs match the shared fixture exactly", () => {
    const fixture = loadFixture();
    expect(Object.values(TutorialAnchorIds).sort()).toEqual([...fixture.staticIds].sort());
  });

  it("dynamic ID prefixes match the shared fixture exactly", () => {
    const fixture = loadFixture();
    expect(TutorialAnchorDynamicPrefixes).toEqual(fixture.dynamicPrefixes);
  });
});

// 空白入りIDはトークン一致セレクタで永久に解決しないため、生成側で落ちることを固定する
// A whitespace-bearing ID never resolves through the token-match selector, so pin that generation itself fails
describe("dynamic anchor ID generation", () => {
  it("rejects whitespace in the generated ID", () => {
    expect(() => inventoryItemAnchorId("a0000000-0000-4000-8000-000000000001 extra")).toThrow(/whitespace/);
    expect(() => buildMenuEntryAnchorId("block", "iron chest")).toThrow(/whitespace/);
  });

  it("lowercases guid-suffixed IDs and keeps the numeric suffixes intact", () => {
    expect(inventoryItemAnchorId("A0000000-0000-4000-8000-000000000001")).toBe(
      "inventory.item-a0000000-0000-4000-8000-000000000001",
    );
    expect(equipmentSlotAnchorId(2)).toBe("equipment.slot-2");
    expect(recipeItemAnchorId(42)).toBe("recipe.item-42");
  });
});
