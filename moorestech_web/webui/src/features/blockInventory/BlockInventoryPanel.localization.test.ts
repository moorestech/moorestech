import { readFileSync } from "node:fs";
// @ts-expect-error -- The build script is intentionally a plain ESM module.
import { parseLocalizationCsv } from "../../../scripts/generate-localization-keys.mjs";
import { createElement } from "react";
import { act, create, type ReactTestRenderer } from "react-test-renderer";
import { describe, expect, it, vi } from "vitest";
import { blockNameKey, L } from "@/shared/i18n";
import { setDictionaries } from "@/shared/i18n/i18nStore";

const topicState = vi.hoisted(() => ({
  blockInventory: {
    error: undefined as "openFailed" | undefined,
    open: true,
    source: "block",
    blockType: "Chest",
    identifier: "block:1",
    blockGuid: "abcdefab-cdef-4bcd-8fab-cdefabcdefab",
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
  resolveBlockComponent: () => ({ body: () => createElement("mock-block-body") }),
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

    // payload固定で辞書通知へ追従
    // Follow dictionary notifications without replacing the payload
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
  it("取得失敗はブロック本文を隠し、失敗文を表示する", () => {
    topicState.blockInventory.error = "openFailed";
    const csv = parseLocalizationCsv(readFileSync(new URL("../../../../../Localization/localization.csv", import.meta.url), "utf8"));
    const failure = csv.rows.find((row: { key: string }) => row.key === L.ui.blockInventory.inventoryOpenFailed);
    const text = failure.texts[csv.languageCodes.indexOf("japanese")];
    expect(text).toBe("インベントリを取得できませんでした");
    expect(failure.source).toBe("Could not load inventory");
    act(() => setDictionaries("japanese", { [L.ui.blockInventory.inventoryOpenFailed]: text, [L.ui.common.close]: "閉じる", [blockNameKey(topicState.blockInventory.blockGuid)]: "タンク" }, {}, {}));
    let renderer: ReactTestRenderer;
    act(() => { renderer = create(createElement(BlockInventoryPanel)); });
    expect(renderer!.root.findByProps({ "data-testid": "block-inventory-error" }).children).toEqual([text]);
    expect(renderer!.root.findAllByType("mock-block-body" as never)).toHaveLength(0);
    act(() => renderer!.unmount());
    topicState.blockInventory.error = undefined;
  });

});
