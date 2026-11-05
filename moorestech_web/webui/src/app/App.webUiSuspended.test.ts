// @vitest-environment jsdom
// UITK が主パネルを出す間（R1）は stage も Portal も描かず、透過の空要素だけを残すことを検証
// Verifies that while UITK shows a main panel (R1) neither the stage nor portals render, leaving only a transparent empty element
import { createElement } from "react";
import { act, create, type ReactTestRenderer } from "react-test-renderer";
import { afterEach, describe, expect, it, vi } from "vitest";

const host = vi.hoisted(() => ({
  topics: {} as Record<string, unknown>,
}));

vi.mock("@/bridge", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/bridge")>();
  return {
    ...actual,
    useConnectionStatus: () => "connected",
    useTopicSelector: (topic: string, selector: (value: unknown) => unknown) => selector(host.topics[topic] ?? null),
  };
});
vi.mock("@/shared/uiState/inputExclusivity/useWebInputExclusivity", () => ({ useWebInputExclusivity: () => {} }));
vi.mock("@/shared/i18n", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/shared/i18n")>()),
  useI18n: () => ({ status: "ready", t: (key: string) => key }),
}));

import { Topics } from "@/bridge";
import App from "./App";

describe("App の Web 全面休止（R1）", () => {
  afterEach(() => {
    host.topics = {};
  });

  it("webUiSuspended の間は stage を描かず透過の空要素だけを返す", () => {
    host.topics[Topics.uiVisibility] = { visible: true };
    host.topics[Topics.uiState] = { state: "PauseMenu", keyHints: [], webUiSuspended: true };

    let renderer!: ReactTestRenderer;
    act(() => { renderer = create(createElement(App)); });

    expect(renderer.root.findAllByProps({ "data-testid": "app-stage" })).toHaveLength(0);
    const tree = renderer.toJSON();
    expect(tree).not.toBeNull();
    expect(Array.isArray(tree)).toBe(false);
    const root = tree as { type: string; props: Record<string, unknown>; children: unknown };
    expect(root.type).toBe("div");
    expect(root.props["data-web-ui-transparent"]).toBe(true);
    expect(root.children).toBeNull();
    act(() => renderer.unmount());
  });
});
