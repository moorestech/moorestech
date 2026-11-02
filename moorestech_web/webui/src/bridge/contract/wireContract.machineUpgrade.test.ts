import { describe, expect, it } from "vitest";
import { Topics } from "../transport/protocol";
import type { MachineRecipesData } from "./payloadTypes";
import { MachineRecipeLevelInheritanceSchema } from "./schemas/machineUpgrade";
import { parseTopicPayload } from "./validators";
import { loadFixture } from "./wireFixtures.test-helper";

// C# の MachineUpgradeWireContractTest と同じフィクスチャを読む
// Reads the same fixtures as the C# MachineUpgradeWireContractTest
describe("machine upgrade wire fixtures", () => {
  it("machine_upgrade.current の上限を受け取る", () => {
    const data = loadFixture("machine_upgrade.json");
    expect(parseTopicPayload(Topics.machineUpgrade, data).valid).toBe(true);
  });

  it("継承つき機械レシピの levelInheritance を受け取る", () => {
    const data = loadFixture("machine_recipe_level_inheritance.json");
    expect(parseTopicPayload(Topics.machineRecipes, data).valid).toBe(true);
    const recipe = (data as MachineRecipesData).recipes[0];
    expect(recipe.levelInheritance.kind).toBe("inheritUpToUnlocked");
    expect(recipe.levelInheritance.maxCraftableLevel).toBe(2);
  });

  it("levelInheritance が欠けたレシピは拒否する", () => {
    const data = loadFixture("machine_recipe_level_inheritance.json") as { recipes: Record<string, unknown>[] };
    delete data.recipes[0].levelInheritance;
    expect(parseTopicPayload(Topics.machineRecipes, data).valid).toBe(false);
  });
});

describe("MachineRecipeLevelInheritanceSchema", () => {
  const none = { kind: "none", sourceInputIndex: -1, sourceItemIds: [], targetOutputIndex: -1, targetItemIds: [], maxCraftableLevel: 0 };
  const inherit = { kind: "inherit", sourceInputIndex: 0, sourceItemIds: [11, 12], targetOutputIndex: 0, targetItemIds: [21, 22], maxCraftableLevel: 2 };

  // none は番兵値（-1・空配列・0）だけを受け、継承ありは none の番兵値を受けない
  // none accepts only its sentinels (-1, empty arrays, 0), and inheriting kinds reject none's sentinels
  it.each([
    [none, true],
    [{ ...none, targetItemIds: [999] }, false],
    [{ ...none, sourceItemIds: [1] }, false],
    [{ ...none, maxCraftableLevel: 2 }, false],
    [{ ...none, sourceInputIndex: 0 }, false],
    [{ ...none, targetOutputIndex: 0 }, false],
    [inherit, true],
    [{ ...inherit, kind: "inheritUpToUnlocked" }, true],
    [{ ...inherit, sourceInputIndex: -1 }, false],
    [{ ...inherit, targetOutputIndex: -1 }, false],
    [{ ...inherit, sourceItemIds: [] }, false],
    [{ ...inherit, kind: "inheritUpToUnlocked", targetItemIds: [] }, false],
    [{ ...inherit, kind: "inheritUpToUnlocked", maxCraftableLevel: 0 }, false],
    [{ ...inherit, maxCraftableLevel: 1.5 }, false],
    [{ ...inherit, kind: "copy" }, false],
  ])("%j の受理は %s", (value, expected) => {
    expect(MachineRecipeLevelInheritanceSchema.safeParse(value).success).toBe(expected);
  });
});
