// 機械レシピ選択行の絞り込み、代表アイコン、選択状態を検証する
// Verifies machine recipe selection filtering, representative icons, and selection state
import { describe, expect, it } from "vitest";
import type { MachineRecipe } from "@/bridge";
import { buildMachineRecipeSelectionRows } from "./machineRecipeSelectionLogic";

const emptyGuid = "00000000-0000-0000-0000-000000000000";

function recipe(recipeGuid: string, blockGuid: string, outputItems = [{ itemId: 2, count: 3 }], inputItems = [{ itemId: 1, count: 4 }]) {
  return {
    recipeGuid, blockGuid, blockId: 10, blockName: "Machine", time: 1, inputItems, outputItems,
  } as MachineRecipe;
}

describe("buildMachineRecipeSelectionRows", () => {
  it("blockGuidが一致するレシピだけを残す", () => {
    const rows = buildMachineRecipeSelectionRows([
      recipe("same", "block-a"),
      recipe("other", "block-b"),
    ], "block-a", emptyGuid);

    expect(rows.map((row) => row.recipeGuid)).toEqual(["same"]);
  });

  it("空GUIDでない選択中レシピだけをハイライトする", () => {
    const rows = buildMachineRecipeSelectionRows([
      recipe("selected", "block-a"),
      recipe("idle", "block-a"),
    ], "block-a", "selected");

    expect(rows.map((row) => row.selected)).toEqual([true, false]);
  });

  it.each([emptyGuid, "", undefined])("未選択値 %s ではハイライトしない", (selectedRecipeGuid) => {
    const rows = buildMachineRecipeSelectionRows([recipe(emptyGuid, "block-a")], "block-a", selectedRecipeGuid);

    expect(rows[0].selected).toBe(false);
  });

  it("一致するレシピがなければ空配列を返す", () => {
    expect(buildMachineRecipeSelectionRows([recipe("other", "block-b")], "block-a", emptyGuid)).toEqual([]);
  });

  it("出力先頭を優先し、出力なしは入力先頭へフォールバックし、双方なしは除外する", () => {
    const rows = buildMachineRecipeSelectionRows([
      recipe("output", "block-a"),
      recipe("input", "block-a", [], [{ itemId: 7, count: 8 }]),
      recipe("empty", "block-a", [], []),
    ], "block-a", emptyGuid);

    expect(rows).toEqual([
      { recipeGuid: "output", iconItemId: 2, iconCount: 3, selected: false },
      { recipeGuid: "input", iconItemId: 7, iconCount: 8, selected: false },
    ]);
  });
});
