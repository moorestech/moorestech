import { createElement } from "react";
import { act, create, type ReactTestRenderer } from "react-test-renderer";
import { describe, expect, it, vi } from "vitest";
import { blockNameKey, L } from "@/shared/i18n";
import { setDictionaries } from "@/shared/i18n/i18nStore";

const blockGuid = "abcdefab-cdef-4bcd-8fab-cdefabcdefab";
const topicState = vi.hoisted(() => ({
  placement: {
    selectedTargetType: "block",
    selectedBlockGuid: "abcdefab-cdef-4bcd-8fab-cdefabcdefab",
    height: 2,
    unavailableReason: "",
  } as
    | { selectedTargetType: "block"; selectedBlockGuid: string; height: number; unavailableReason: string }
    | { selectedTargetType: "blueprintCopy"; height: number; unavailableReason: string }
    | { selectedTargetType: "raw"; selectedName: string; height: number; unavailableReason: string },
}));

vi.mock("@/bridge", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/bridge")>()),
  useTopic: () => topicState.placement,
}));
vi.mock("@/shared/ui", () => ({
  GamePanel: ({ children, ...props }: { children: unknown }) =>
    createElement("mock-game-panel", props, children as never),
  FadeRule: () => createElement("mock-fade-rule"),
}));
vi.mock("@/shared/tutorialAnchor", () => ({
  tutorialAnchor: () => ({}),
  TutorialAnchorIds: { placementHud: "placement-hud" },
}));

import { PlacementModeHud } from "./PlacementModeHud";

describe("PlacementModeHud localization", () => {
  it("block Guidを現在辞書の表示名へ解決する", () => {
    setDictionary({ [blockNameKey(blockGuid)]: "Localized Assembler" });

    let renderer: ReactTestRenderer;
    act(() => {
      renderer = create(createElement(PlacementModeHud));
    });

    expect(detailTexts(renderer!)[0]).toBe("Selected: Localized Assembler");
    act(() => renderer!.unmount());
  });

  it("block以外のraw表示名をそのまま維持する", () => {
    topicState.placement = {
      selectedTargetType: "raw",
      selectedName: "My Blueprint",
      height: 2,
      unavailableReason: "",
    };
    setDictionary({});

    let renderer: ReactTestRenderer;
    act(() => {
      renderer = create(createElement(PlacementModeHud));
    });

    expect(detailTexts(renderer!)[0]).toBe("Selected: My Blueprint");
    act(() => renderer!.unmount());
  });

  it("Blueprint Copyをtyped UI keyで解決する", () => {
    topicState.placement = {
      selectedTargetType: "blueprintCopy",
      height: 2,
      unavailableReason: "",
    };
    setDictionary({ [L.ui.buildMenu.blueprintCopy]: "Copy Blueprint" });

    let renderer: ReactTestRenderer;
    act(() => {
      renderer = create(createElement(PlacementModeHud));
    });

    expect(detailTexts(renderer!)[0]).toBe("Selected: Copy Blueprint");
    act(() => renderer!.unmount());
  });
});

function setDictionary(content: Readonly<Record<string, string>>): void {
  setDictionaries("english", {
    [L.ui.modeHud.placementModeTitle]: "Placement",
    [L.ui.modeHud.selectedBlock]: "Selected: {name}",
    [L.ui.modeHud.placementHeight]: "Height: {height}",
    ...content,
  }, {}, {});
}

function detailTexts(renderer: ReactTestRenderer): string[] {
  return renderer.root
    .findAll((node) => node.props["data-testid"] === "operation-mode-detail")
    .map((node) => String(node.children[0]));
}
