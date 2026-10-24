import { describe, expect, it } from "vitest";
import { Topics } from "../transport/protocol";
import type { MachineRecipesData } from "./payloadTypes";
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
