import { createElement } from "react";
import { act, create } from "react-test-renderer";
import { describe, expect, it, vi } from "vitest";
import type { BlockInventoryOpen, MachineDetailData } from "@/bridge";

const recipeGuid = "84000000-0000-4000-8000-000000000001";
const otherRecipeGuid = "84000000-0000-4000-8000-000000000002";
const blockGuid = "85000000-0000-4000-8000-000000000001";
const otherBlockGuid = "85000000-0000-4000-8000-000000000002";
const emptyGuid = "00000000-0000-0000-0000-000000000000";

vi.mock("@/bridge", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/bridge")>()),
  useTopic: () => ({
    recipes: [
      {
        recipeGuid, blockGuid, blockId: 10, time: 7,
        inputItems: [{ itemId: 1, count: 2 }], outputItems: [{ itemId: 2, count: 1 }],
        inputFluids: [], outputFluids: [],
        levelInheritance: { kind: "none", sourceInputIndex: -1, sourceItemIds: [], targetOutputIndex: -1, targetItemIds: [], maxCraftableLevel: 0 },
      },
      {
        recipeGuid: otherRecipeGuid, blockGuid, blockId: 10, time: 9,
        inputItems: [{ itemId: 1, count: 3 }], outputItems: [{ itemId: 3, count: 1 }],
        inputFluids: [], outputFluids: [],
        levelInheritance: { kind: "none", sourceInputIndex: -1, sourceItemIds: [], targetOutputIndex: -1, targetItemIds: [], maxCraftableLevel: 0 },
      },
    ],
  }),
}));
vi.mock("@/shared/i18n", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/shared/i18n")>()),
  useI18n: () => ({ t: (key: string) => key }),
}));
vi.mock("@mantine/core", () => ({
  Group: ({ children, ...props }: { children: unknown }) => createElement("mock-group", props, children as never),
  Stack: ({ children, ...props }: { children: unknown }) => createElement("mock-stack", props, children as never),
  ScrollArea: ({ children, ...props }: { children: unknown }) => createElement("mock-scroll-area", props, children as never),
}));
vi.mock("./LackHighlightText", () => ({ default: (props: object) => createElement("mock-lack", props) }));
vi.mock("./rows/PowerRateText", () => ({ default: (props: object) => createElement("mock-power", props) }));
vi.mock("./machine/MachineInventoryBody", () => ({ default: (props: object) => createElement("mock-inventory-body", props) }));
vi.mock("./machine/recipeSelection/MachineRecipeSelectionList", () => ({ default: (props: object) => createElement("mock-recipe-selection-list", props) }));
vi.mock("./machine/SelectedRecipeHeader", () => ({ default: (props: object) => createElement("mock-selected-recipe-header", props) }));

import MachineSection from "./MachineSection";

function machine(selectedRecipeGuid: string, machineBlockGuid: string, currentState: MachineDetailData["currentState"] = "idle"): MachineDetailData {
  return {
    recipeGuid: emptyGuid, selectedRecipeGuid, blockGuid: machineBlockGuid, recipeTime: 7,
    outputItems: [], currentState, currentPower: 0, requestPower: 0, outputSpaceShort: false,
    slotLayout: { input: 2, output: 1, module: 0, inputTank: 0 },
    slotBindings: [], tankBindings: [],
  };
}
// 描画済みの不足トーン文言を testId で拾う（コンポーネント層とホスト層の二重一致を避ける）
// Pick rendered lack-tone texts by testId, avoiding the double match of component and host layers
function lackTexts(tree: ReturnType<typeof create>, testId: string) {
  return tree.root.findAllByType("mock-lack" as never).filter((node) => node.props.testId === testId);
}
const data = { open: true, itemSlots: [], fluidSlots: [], progress: null } as unknown as BlockInventoryOpen;

describe("MachineSection", () => {
  it("未選択機械はレシピ選択リストを出し、インベントリ本体を出さない", () => {
    const tree = create(createElement(MachineSection, { data, leadingContent: null, fillsPanelHeight: true, machine: machine(emptyGuid, blockGuid) }));
    expect(tree.root.findAllByType("mock-recipe-selection-list" as never)).toHaveLength(1);
    expect(tree.root.findAllByType("mock-inventory-body" as never)).toHaveLength(0);
  });

  it("選択済機械はヘッダ＋本体を出し、ヘッダのonChangeRecipeでリストへ戻り、selectedRecipeGuidが実際に変わるまで本体へ戻らない", () => {
    const tree = create(createElement(MachineSection, { data, leadingContent: null, fillsPanelHeight: true, machine: machine(recipeGuid, blockGuid) }));
    expect(tree.root.findAllByType("mock-inventory-body" as never)).toHaveLength(1);
    const header = tree.root.findByType("mock-selected-recipe-header" as never);
    act(() => header.props.onChangeRecipe());
    expect(tree.root.findAllByType("mock-recipe-selection-list" as never)).toHaveLength(1);
    expect(tree.root.findAllByType("mock-inventory-body" as never)).toHaveLength(0);

    // 別レシピを要求してもサーバーの選択が追いつくまでは選択モードのまま(C14: 拒否時に戻らない)
    // Requesting another recipe keeps selection mode until the server's selection catches up (C14: a rejection never returns)
    const list = tree.root.findByType("mock-recipe-selection-list" as never);
    act(() => list.props.onSelected(otherRecipeGuid));
    act(() => { tree.update(createElement(MachineSection, { data, leadingContent: null, fillsPanelHeight: true, machine: machine(recipeGuid, blockGuid) })); });
    expect(tree.root.findAllByType("mock-recipe-selection-list" as never)).toHaveLength(1);
    expect(tree.root.findAllByType("mock-inventory-body" as never)).toHaveLength(0);

    // 要求したレシピがサーバーの選択になったら本体へ戻る
    // Returns to the inventory body once the requested recipe becomes the server's selection
    act(() => { tree.update(createElement(MachineSection, { data, leadingContent: null, fillsPanelHeight: true, machine: machine(otherRecipeGuid, blockGuid) })); });
    expect(tree.root.findAllByType("mock-inventory-body" as never)).toHaveLength(1);
    expect(tree.root.findAllByType("mock-recipe-selection-list" as never)).toHaveLength(0);
  });

  it("同一レシピを選び直しても選択モードから戻れる（要求GUID一致で閉じる）", () => {
    const tree = create(createElement(MachineSection, { data, leadingContent: null, fillsPanelHeight: true, machine: machine(recipeGuid, blockGuid) }));
    const header = tree.root.findByType("mock-selected-recipe-header" as never);
    act(() => header.props.onChangeRecipe());
    expect(tree.root.findAllByType("mock-recipe-selection-list" as never)).toHaveLength(1);

    // 選択中と同じレシピを選び直す。selectedRecipeGuidは変わらないが要求と一致するので閉じる
    // Re-picking the already-selected recipe leaves selectedRecipeGuid unchanged, but it matches the request so the mode closes
    const list = tree.root.findByType("mock-recipe-selection-list" as never);
    act(() => list.props.onSelected(recipeGuid));
    expect(tree.root.findAllByType("mock-inventory-body" as never)).toHaveLength(1);
    expect(tree.root.findAllByType("mock-recipe-selection-list" as never)).toHaveLength(0);
  });

  it("レシピ0件の機械はヘッダもリストも出さず本体だけ出す", () => {
    const tree = create(createElement(MachineSection, { data, leadingContent: null, fillsPanelHeight: true, machine: machine(emptyGuid, otherBlockGuid) }));
    expect(tree.root.findAllByType("mock-inventory-body" as never)).toHaveLength(1);
    expect(tree.root.findAllByType("mock-recipe-selection-list" as never)).toHaveLength(0);
    expect(tree.root.findAllByType("mock-selected-recipe-header" as never)).toHaveLength(0);
  });

  it("停止中は充足率テキストを出さず状態ラベルだけを見せる", () => {
    const halted = create(createElement(MachineSection, { data, leadingContent: null, fillsPanelHeight: true, machine: machine(recipeGuid, blockGuid, "halted") }));
    expect(halted.root.findAllByProps({ testId: "machine-power-rate" })).toHaveLength(0);

    const processing = create(createElement(MachineSection, { data, leadingContent: null, fillsPanelHeight: true, machine: machine(recipeGuid, blockGuid, "processing") }));
    expect(processing.root.findAllByProps({ testId: "machine-power-rate" }).length).toBeGreaterThan(0);
  });

  it("歯車駆動の機械は充足率に不足赤を許さず、電力駆動の機械は許す（ADR 0056）", () => {
    const gearData = { ...data, gear: { role: "consumer", currentRpm: 5, currentTorque: 1, baseRpm: 10 } } as unknown as BlockInventoryOpen;
    const gearDriven = create(createElement(MachineSection, { data: gearData, leadingContent: null, fillsPanelHeight: true, machine: machine(recipeGuid, blockGuid, "processing") }));
    expect(gearDriven.root.findByType("mock-power" as never).props.highlightShortage).toBe(false);

    const electricDriven = create(createElement(MachineSection, { data, leadingContent: null, fillsPanelHeight: true, machine: machine(recipeGuid, blockGuid, "processing") }));
    expect(electricDriven.root.findByType("mock-power" as never).props.highlightShortage).toBe(true);
  });

  // 大型面だけ本文をスクローラへ入れる
  // Only the height-determining panel puts the body in a scroller; auto-height small panels keep their content size
  it("インベントリ本文は大型面でだけスクローラに入る", () => {
    const large = create(createElement(MachineSection, { data, leadingContent: null, fillsPanelHeight: true, machine: machine(recipeGuid, blockGuid) }));
    expect(large.root.findByType("mock-scroll-area" as never).findAllByType("mock-inventory-body" as never)).toHaveLength(1);
    const small = create(createElement(MachineSection, { data, leadingContent: null, fillsPanelHeight: false, machine: machine(recipeGuid, blockGuid) }));
    expect(small.root.findAllByType("mock-scroll-area" as never)).toHaveLength(0);
    expect(small.root.findAllByType("mock-inventory-body" as never)).toHaveLength(1);
  });

  it("出力空き不足の機械は状態ラベルを待機中のままにし、両モードのフッタで理由を不足トーンで出す（F01）", () => {
    const blocked = { ...machine(recipeGuid, blockGuid), outputSpaceShort: true };
    const tree = create(createElement(MachineSection, { data, leadingContent: null, fillsPanelHeight: true, machine: blocked }));
    const reason = lackTexts(tree, "machine-wait-reason");
    expect(reason).toHaveLength(1);
    expect(reason[0].props.insufficient).toBe(true);
    expect(reason[0].props.children).toBe("ui.blockInventory.machineWaitOutputSpaceShort");
    expect(lackTexts(tree, "machine-state-label")[0].props.children).toBe("ui.blockInventory.machineStateIdle");
    const selecting = create(createElement(MachineSection, { data, leadingContent: null, fillsPanelHeight: true, machine: { ...blocked, selectedRecipeGuid: emptyGuid } }));
    expect(selecting.root.findAllByType("mock-recipe-selection-list" as never)).toHaveLength(1);
    expect(lackTexts(selecting, "machine-wait-reason")).toHaveLength(1);
  });

  it("出力空き不足でない機械は理由を出さない（入力切れの待機と区別するため理由は空き不足のときだけ）", () => {
    const tree = create(createElement(MachineSection, { data, leadingContent: null, fillsPanelHeight: true, machine: machine(recipeGuid, blockGuid) }));
    expect(lackTexts(tree, "machine-wait-reason")).toHaveLength(0);
  });
});
