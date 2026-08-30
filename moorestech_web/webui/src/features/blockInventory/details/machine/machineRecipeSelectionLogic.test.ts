// 機械レシピ選択行の絞り込みと選択状態を検証する
// Verifies machine recipe selection row filtering and selection state
import { describe, expect, it } from "vitest";
import type { MachineRecipe } from "@/bridge";
import { buildMachineRecipeSelectionRows } from "./machineRecipeSelectionLogic";

const blockA = "85000000-0000-4000-8000-000000000001";
const blockB = "85000000-0000-4000-8000-000000000002";
const emptyGuid = "00000000-0000-0000-0000-000000000000";
function recipe(recipeGuid: string, blockGuid: string, outputItems = [{ itemId: 2, count: 1 }]): MachineRecipe {
  return { recipeGuid, blockGuid, blockId: 1, time: 1, inputItems: [{ itemId: 1, count: 1 }], outputItems, inputFluids: [], outputFluids: [] };
}

describe("buildMachineRecipeSelectionRows", () => {
  it("開いている機械のレシピだけを選択フラグ付きで返す", () => {
    const a = recipe("84000000-0000-4000-8000-000000000001", blockA);
    const rows = buildMachineRecipeSelectionRows([a, recipe("84000000-0000-4000-8000-000000000002", blockB)], blockA, a.recipeGuid);
    expect(rows).toEqual([{ recipe: a, selected: true }]);
  });

  it("空GUIDはどの行も選択しない", () => {
    const a = recipe("84000000-0000-4000-8000-000000000001", blockA);
    expect(buildMachineRecipeSelectionRows([a], blockA, emptyGuid)[0].selected).toBe(false);
  });

  // 代表出力が無いレシピは名前欄が空文字になるため、行そのものを除外する（C9）
  // A recipe without a representative output would render an empty name; exclude the row entirely (C9)
  it("代表出力（先頭の生産物）が無いレシピは除外する", () => {
    const noOutput = recipe("84000000-0000-4000-8000-000000000003", blockA, []);
    expect(buildMachineRecipeSelectionRows([noOutput], blockA, emptyGuid)).toEqual([]);
  });

  it("一致するレシピがなければ空配列を返す", () => {
    expect(buildMachineRecipeSelectionRows([recipe("84000000-0000-4000-8000-000000000001", blockB)], blockA, emptyGuid)).toEqual([]);
  });
});
