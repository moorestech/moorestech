import { describe, expect, it } from "vitest";
import type { MachineRecipeLevelInheritance } from "@/bridge";
import { L } from "@/shared/i18n";
import { levelInheritanceBadgeText } from "./levelInheritanceBadge";

const base: MachineRecipeLevelInheritance = {
  kind: "none", sourceInputIndex: -1, sourceItemIds: [], targetOutputIndex: -1, targetItemIds: [], maxCraftableLevel: 0,
};

describe("levelInheritanceBadgeText", () => {
  it("継承しないレシピにはバッジを出さない", () => {
    expect(levelInheritanceBadgeText(base)).toBeNull();
  });

  it("上限なしの継承は LvN→LvN", () => {
    const inherit = { ...base, kind: "inherit" as const, sourceInputIndex: 0, sourceItemIds: [11, 12, 13], targetOutputIndex: 0, targetItemIds: [21, 22, 23], maxCraftableLevel: 3 };
    expect(levelInheritanceBadgeText(inherit)).toEqual({ key: L.ui.recipe.levelInheritance, values: {} });
  });

  it("上限つきの継承は今の上限を差し込む", () => {
    const capped = { ...base, kind: "inheritUpToUnlocked" as const, sourceInputIndex: 0, sourceItemIds: [11, 12, 13], targetOutputIndex: 0, targetItemIds: [21, 22, 23], maxCraftableLevel: 2 };
    expect(levelInheritanceBadgeText(capped)).toEqual({ key: L.ui.recipe.levelInheritanceCapped, values: { level: 2 } });
  });
});
