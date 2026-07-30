import { createElement } from "react";
import { act, create, type ReactTestRenderer } from "react-test-renderer";
import { describe, expect, it, vi } from "vitest";
import { blockNameKey, L } from "@/shared/i18n";
import { setDictionaries } from "@/shared/i18n/i18nStore";

const topicState = vi.hoisted(() => ({
  blockInventory: {
    open: true,
    source: "block",
    blockType: "Chest",
    identifier: "block:1",
    blockGuid: "ABCDEFAB-CDEF-ABCD-EFAB-CDEFABCDEFAB",
    itemSlots: [],
    fluidSlots: [],
  },
  machineRecipes: { recipes: [] },
}));

vi.mock("@/bridge", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/bridge")>()),
  useTopic: (topic: string) => topic === "block_inventory.current"
    ? topicState.blockInventory
    : topicState.machineRecipes,
  dispatchAction: vi.fn(),
}));
vi.mock("@/shared/ui", () => ({
  GamePanel: ({ children, ...props }: { children: unknown }) => createElement("mock-game-panel", props, children as never),
  IconButton: (props: object) => createElement("mock-icon-button", props),
}));
vi.mock("./registry/blockComponentRegistry", () => ({
  resolveBlockComponent: () => () => createElement("mock-block-body"),
}));
vi.mock("./details/machine/machineRecipeSelectionLogic", () => ({
  buildMachineRecipeSelectionRows: () => [],
}));
vi.mock("@/shared/tutorialAnchor", () => ({
  tutorialAnchor: () => ({}),
  TutorialAnchorIds: { inventoryCloseButton: "inventory-close" },
}));

import BlockInventoryPanel from "./BlockInventoryPanel";

describe("BlockInventoryPanel localization", () => {
  it("blockGuidの導出キーをfallback解決し、辞書世代の変更でタイトルを再描画する", () => {
    const key = blockNameKey(topicState.blockInventory.blockGuid);
    act(() => setDictionaries("japanese", {}, {
      [key]: "Fallback Machine",
      [L.ui.common.close]: "Close",
    }, { [key]: "Source Machine" }));
    let renderer: ReactTestRenderer;
    act(() => {
      renderer = create(createElement(BlockInventoryPanel));
    });

    expect(renderer!.root.findByType("mock-game-panel" as never).props.title).toBe("Fallback Machine");

    // 同じpayloadのまま辞書更新通知だけで表示言語へ追従する
    // Follow the display language from dictionary notifications without replacing the payload
    act(() => setDictionaries("japanese", {
      [key]: "対象言語の機械",
      [L.ui.common.close]: "閉じる",
    }, {}, { [key]: "Source Machine" }));
    expect(renderer!.root.findByType("mock-game-panel" as never).props.title).toBe("対象言語の機械");

    act(() => setDictionaries("japanese", {}, {}, {
      [key]: "Source Machine",
      [L.ui.common.close]: "Close",
    }));
    expect(renderer!.root.findByType("mock-game-panel" as never).props.title).toBe("Source Machine");
  });
});
