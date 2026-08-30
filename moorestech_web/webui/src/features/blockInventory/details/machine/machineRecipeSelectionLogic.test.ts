// 機械レシピ選択行の絞り込みと選択状態を検証する
// Verifies machine recipe selection row filtering and selection state
import { describe, expect, it } from "vitest";
import type { MachineRecipe } from "@/bridge";
import { buildMachineRecipeSelectionRows } from "./machineRecipeSelectionLogic";

const blockA = "85000000-0000-4000-8000-000000000001";
const blockB = "85000000-0000-4000-8000-000000000002";
const emptyGuid = "00000000-0000-0000-0000-000000000000";
function recipe(
  recipeGuid: string,
  blockGuid: string,
  outputItems: MachineRecipe["outputItems"] = [{ itemId: 2, count: 1 }],
  outputFluids: MachineRecipe["outputFluids"] = [],
): MachineRecipe {
  return { recipeGuid, blockGuid, blockId: 1, time: 1, inputItems: [{ itemId: 1, count: 1 }], outputItems, inputFluids: [], outputFluids };
}

describe("buildMachineRecipeSelectionRows", () => {
  it("開いている機械のレシピだけを選択フラグ付きで返す", () => {
    const a = recipe("84000000-0000-4000-8000-000000000001", blockA);
    const rows = buildMachineRecipeSelectionRows([a, recipe("84000000-0000-4000-8000-000000000002", blockB)], blockA, a.recipeGuid);
    expect(rows).toEqual([{ recipe: a, subject: { kind: "item", itemId: 2, count: 1 }, selected: true }]);
  });

  it("空GUIDはどの行も選択しない", () => {
    const a = recipe("84000000-0000-4000-8000-000000000001", blockA);
    expect(buildMachineRecipeSelectionRows([a], blockA, emptyGuid)[0].selected).toBe(false);
  });

  // 代表出力（アイテムも液体も）が無いレシピは名前欄が空文字になるため、行そのものを除外する
  // A recipe without any representative output would render an empty name; exclude the row entirely
  it("代表出力（アイテムも液体も）が無いレシピは除外する", () => {
    const noOutput = recipe("84000000-0000-4000-8000-000000000003", blockA, []);
    expect(buildMachineRecipeSelectionRows([noOutput], blockA, emptyGuid)).toEqual([]);
  });

  // D2/C2回帰: ボイラー・石油蒸留機のような液体のみ出力レシピは、代表を液体へフォールバックして行に残す
  // D2/C2 regression: fluid-only-output recipes (boiler, oil refinery) fall back to a fluid subject and stay in the rows
  it("出力アイテムが無く出力液体のみのレシピは液体を代表にして残す", () => {
    const fluidOnly = recipe(
      "84000000-0000-4000-8000-000000000004",
      blockA,
      [],
      [{ fluidId: 9, fluidGuid: "87000000-0000-4000-8000-000000000001", amount: 100 }],
    );
    expect(buildMachineRecipeSelectionRows([fluidOnly], blockA, emptyGuid)).toEqual([
      { recipe: fluidOnly, subject: { kind: "fluid", fluidGuid: "87000000-0000-4000-8000-000000000001", amount: 100 }, selected: false },
    ]);
  });

  it("一致するレシピがなければ空配列を返す", () => {
    expect(buildMachineRecipeSelectionRows([recipe("84000000-0000-4000-8000-000000000001", blockB)], blockA, emptyGuid)).toEqual([]);
  });
});
