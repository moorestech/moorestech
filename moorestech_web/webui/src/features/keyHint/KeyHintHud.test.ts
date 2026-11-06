// ホスト由来のヒント配列をkbd+文言の順で描き、blockingスキット中は退避することを検証
// Verifies the host-supplied hints render as kbd + text and retreat during a blocking skit
import { createElement } from "react";
import { act, create } from "react-test-renderer";
import { afterEach, describe, expect, it, vi } from "vitest";
import type { UiStateData } from "@/bridge";
import { setDictionaries } from "@/shared/i18n/i18nStore";

const host = vi.hoisted(() => ({
  uiState: null as UiStateData | null,
  blockingSkit: false,
}));

vi.mock("@/bridge", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/bridge")>();
  return {
    ...actual,
    useTopic: (topic: string) => (topic === actual.Topics.uiState ? host.uiState : null),
  };
});
vi.mock("@/shared/uiState", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/shared/uiState")>();
  return { ...actual, useBlockingSkitActive: () => host.blockingSkit };
});

import { KeyHintHud } from "./KeyHintHud";

const tabKeyNameKey = "ui.keyHint.key.tab";
const inventoryTextKey = "ui.keyHint.text.inventory";

function render() {
  let renderer!: ReturnType<typeof create>;
  act(() => { renderer = create(createElement(KeyHintHud)); });
  return renderer;
}

describe("KeyHintHud", () => {
  afterEach(() => {
    host.uiState = null;
    host.blockingSkit = false;
    setDictionaries("english", {}, {}, {});
  });

  it("ヒントをkbd+文言の順で描く", () => {
    setDictionaries("english", { [tabKeyNameKey]: "Tab", [inventoryTextKey]: "Inventory" }, {}, {});
    host.uiState = { state: "GameScreen", keyHints: [{ keyNameKey: tabKeyNameKey, textKey: inventoryTextKey }] };

    const renderer = render();

    const hud = renderer.root.findByProps({ "data-testid": "key-hints" });
    const [kbdChild, textChild] = (hud.children[0] as { children: unknown[] }).children;
    expect((kbdChild as { type: string }).type).toBe("kbd");
    expect((kbdChild as { children: unknown[] }).children).toEqual(["Tab"]);
    expect(textChild).toBe("Inventory");
    act(() => renderer.unmount());
  });

  it("ヒントが空ならHUD自体を描かない", () => {
    host.uiState = { state: "GameScreen", keyHints: [] };

    const renderer = render();

    expect(renderer.root.findAllByProps({ "data-testid": "key-hints" }).length).toBe(0);
    act(() => renderer.unmount());
  });

  it("blockingスキット中は描かない", () => {
    setDictionaries("english", { [tabKeyNameKey]: "Tab", [inventoryTextKey]: "Inventory" }, {}, {});
    host.uiState = { state: "GameScreen", keyHints: [{ keyNameKey: tabKeyNameKey, textKey: inventoryTextKey }] };
    host.blockingSkit = true;

    const renderer = render();

    expect(renderer.root.findAllByProps({ "data-testid": "key-hints" }).length).toBe(0);
    act(() => renderer.unmount());
  });

  it("辞書に無いキーは声高なplaceholderへ落とす", () => {
    host.uiState = { state: "GameScreen", keyHints: [{ keyNameKey: tabKeyNameKey, textKey: inventoryTextKey }] };

    const renderer = render();

    const hud = renderer.root.findByProps({ "data-testid": "key-hints" });
    const [kbdChild, textChild] = (hud.children[0] as { children: unknown[] }).children;
    expect((kbdChild as { children: unknown[] }).children).toEqual([`[!${tabKeyNameKey}]`]);
    expect(textChild).toBe(`[!${inventoryTextKey}]`);
    act(() => renderer.unmount());
  });
});
