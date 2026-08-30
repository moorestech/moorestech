import { createElement } from "react";
import { act, create } from "react-test-renderer";
import { describe, expect, it, vi } from "vitest";
import type { BlockInventoryOpen, MachineDetailData } from "@/bridge";

const recipeGuid = "84000000-0000-4000-8000-000000000001";
const blockGuid = "85000000-0000-4000-8000-000000000001";
const otherBlockGuid = "85000000-0000-4000-8000-000000000002";
const emptyGuid = "00000000-0000-0000-0000-000000000000";

vi.mock("@/bridge", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/bridge")>()),
  useTopic: () => ({
    recipes: [{
      recipeGuid, blockGuid, blockId: 10, time: 7,
      inputItems: [{ itemId: 1, count: 2 }], outputItems: [{ itemId: 2, count: 1 }],
      inputFluids: [], outputFluids: [],
    }],
  }),
}));
vi.mock("@/shared/i18n", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/shared/i18n")>()),
  useI18n: () => ({ t: (key: string) => key }),
}));
vi.mock("@mantine/core", () => ({
  Group: ({ children, ...props }: { children: unknown }) => createElement("mock-group", props, children as never),
  Stack: ({ children, ...props }: { children: unknown }) => createElement("mock-stack", props, children as never),
}));
vi.mock("./LackHighlightText", () => ({ default: (props: object) => createElement("mock-lack", props) }));
vi.mock("./PowerRateText", () => ({ default: (props: object) => createElement("mock-power", props) }));
vi.mock("./machine/MachineInventoryBody", () => ({ default: (props: object) => createElement("mock-inventory-body", props) }));
vi.mock("./machine/recipeSelection/MachineRecipeSelectionList", () => ({ default: (props: object) => createElement("mock-recipe-selection-list", props) }));
vi.mock("./machine/SelectedRecipeHeader", () => ({ default: (props: object) => createElement("mock-selected-recipe-header", props) }));

import MachineSection from "./MachineSection";

function machine(selectedRecipeGuid: string, machineBlockGuid: string, currentState: MachineDetailData["currentState"] = "idle"): MachineDetailData {
  return {
    recipeGuid: emptyGuid, selectedRecipeGuid, blockGuid: machineBlockGuid, recipeTime: 7,
    outputItems: [], currentState, currentPower: 0, requestPower: 0,
    slotLayout: { input: 2, output: 1, module: 0, inputTank: 0 },
  };
}
const data = { open: true, itemSlots: [], fluidSlots: [], progress: null } as unknown as BlockInventoryOpen;

describe("MachineSection", () => {
  it("未選択機械はレシピ選択リストを出し、インベントリ本体を出さない", () => {
    const tree = create(createElement(MachineSection, { data, machine: machine(emptyGuid, blockGuid) }));
    expect(tree.root.findAllByType("mock-recipe-selection-list" as never)).toHaveLength(1);
    expect(tree.root.findAllByType("mock-inventory-body" as never)).toHaveLength(0);
  });

  it("選択済機械はヘッダ＋本体を出し、ヘッダのonChangeRecipeでリストへ戻り、onSelectedで本体へ戻る", () => {
    const tree = create(createElement(MachineSection, { data, machine: machine(recipeGuid, blockGuid) }));
    expect(tree.root.findAllByType("mock-inventory-body" as never)).toHaveLength(1);
    const header = tree.root.findByType("mock-selected-recipe-header" as never);
    act(() => header.props.onChangeRecipe());
    const list = tree.root.findByType("mock-recipe-selection-list" as never);
    expect(tree.root.findAllByType("mock-inventory-body" as never)).toHaveLength(0);
    act(() => list.props.onSelected());
    expect(tree.root.findAllByType("mock-inventory-body" as never)).toHaveLength(1);
  });

  it("レシピ0件の機械はヘッダもリストも出さず本体だけ出す", () => {
    const tree = create(createElement(MachineSection, { data, machine: machine(emptyGuid, otherBlockGuid) }));
    expect(tree.root.findAllByType("mock-inventory-body" as never)).toHaveLength(1);
    expect(tree.root.findAllByType("mock-recipe-selection-list" as never)).toHaveLength(0);
    expect(tree.root.findAllByType("mock-selected-recipe-header" as never)).toHaveLength(0);
  });

  it("停止中は充足率テキストを出さず状態ラベルだけを見せる", () => {
    const halted = create(createElement(MachineSection, { data, machine: machine(recipeGuid, blockGuid, "halted") }));
    expect(halted.root.findAllByProps({ testId: "machine-power-rate" })).toHaveLength(0);

    const processing = create(createElement(MachineSection, { data, machine: machine(recipeGuid, blockGuid, "processing") }));
    expect(processing.root.findAllByProps({ testId: "machine-power-rate" }).length).toBeGreaterThan(0);
  });
});
