// 機械レシピ選択行の絞り込み、代表アイコン、選択状態を検証する
// Verifies machine recipe selection filtering, representative icons, and selection state
import { describe, expect, it } from "vitest";
import type { MachineRecipe } from "@/bridge";
import { buildMachineRecipeSelectionRows, machineInitialTab } from "./machineRecipeSelectionLogic";

const emptyGuid = "00000000-0000-0000-0000-000000000000";
const blockA = "85000000-0000-4000-8000-000000000001";
const blockB = "85000000-0000-4000-8000-000000000002";
const recipeA = "84000000-0000-4000-8000-000000000001";
const recipeB = "84000000-0000-4000-8000-000000000002";
const recipeC = "84000000-0000-4000-8000-000000000003";

function recipe(recipeGuid: string, blockGuid: string, outputItems = [{ itemId: 2, count: 3 }], inputItems = [{ itemId: 1, count: 4 }]) {
  return {
    recipeGuid, blockGuid, blockId: 10, time: 1, inputItems, outputItems,
  } as MachineRecipe;
}

describe("buildMachineRecipeSelectionRows", () => {
  it("blockGuidが一致するレシピだけを残す", () => {
    const rows = buildMachineRecipeSelectionRows([
      recipe(recipeA, blockA),
      recipe(recipeB, blockB),
    ], blockA, emptyGuid);

    expect(rows.map((row) => row.recipeGuid)).toEqual([recipeA]);
  });

  it("空GUIDでない選択中レシピだけをハイライトする", () => {
    const rows = buildMachineRecipeSelectionRows([
      recipe(recipeA, blockA),
      recipe(recipeB, blockA),
    ], blockA, recipeA);

    expect(rows.map((row) => row.selected)).toEqual([true, false]);
  });

  it.each([emptyGuid, "", undefined])("未選択値 %s ではハイライトしない", (selectedRecipeGuid) => {
    const rows = buildMachineRecipeSelectionRows([recipe(emptyGuid, blockA)], blockA, selectedRecipeGuid);

    expect(rows[0].selected).toBe(false);
  });

  it("一致するレシピがなければ空配列を返す", () => {
    expect(buildMachineRecipeSelectionRows([recipe(recipeA, blockB)], blockA, emptyGuid)).toEqual([]);
  });

  it("出力先頭を優先し、出力なしは入力先頭へフォールバックし、双方なしは除外する", () => {
    const rows = buildMachineRecipeSelectionRows([
      recipe(recipeA, blockA),
      recipe(recipeB, blockA, [], [{ itemId: 7, count: 8 }]),
      recipe(recipeC, blockA, [], []),
    ], blockA, emptyGuid);

    expect(rows).toEqual([
      { recipeGuid: recipeA, iconItemId: 2, iconCount: 3, selected: false },
      { recipeGuid: recipeB, iconItemId: 7, iconCount: 8, selected: false },
    ]);
  });
});

describe("machineInitialTab", () => {
  it.each([
    { guid: null, tab: "recipes" },
    { guid: undefined, tab: "recipes" },
    { guid: "00000000-0000-0000-0000-000000000000", tab: "recipes" },
    { guid: "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb", tab: "inventory" },
  ])("selectedRecipeGuid=$guid → $tab", ({ guid, tab }) => {
    expect(machineInitialTab(guid)).toBe(tab);
  });
});
