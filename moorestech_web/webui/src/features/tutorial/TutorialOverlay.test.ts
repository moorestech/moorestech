// ドラッグガイド矢印の中核ゲート（両anchor ready時のみ描画）を検証する
// Verifies the drag-guide's core gate: it renders only while both anchors are ready
import { createElement } from "react";
import { act, create } from "react-test-renderer";
import { afterEach, describe, expect, it, vi } from "vitest";
import type { ResolvedAnchor } from "@/shared/tutorialAnchor";
import type { TutorialPresentationData } from "@/bridge";

const mockState = vi.hoisted(() => ({
  presentation: null as TutorialPresentationData | null,
  // anchorId -> このanchorを購読しているリスナー集合
  // anchorId -> the set of listeners subscribed to it
  listeners: new Map<string, Set<(value: ResolvedAnchor) => void>>(),
}));

vi.mock("@/bridge", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/bridge")>();
  return {
    ...actual,
    useTopic: () => mockState.presentation,
    dispatchAction: vi.fn(),
  };
});

// 実DOM解決を持たないフェイクレジストリ。テストからanchorIdごとに値を直接プッシュする
// Fake registry with no real DOM resolution; the test pushes values per anchorId directly
vi.mock("@/shared/tutorialAnchor", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/shared/tutorialAnchor")>();
  return {
    ...actual,
    TutorialAnchorRegistry: class {
      subscribe(anchorId: string, listener: (value: ResolvedAnchor) => void) {
        const set = mockState.listeners.get(anchorId) ?? new Set();
        set.add(listener);
        mockState.listeners.set(anchorId, set);
        return () => set.delete(listener);
      }
      dispose() {}
    },
  };
});

import { TutorialOverlay } from "./TutorialOverlay";

const ready = (left: number): ResolvedAnchor => ({
  status: "ready", reason: "mounted",
  rect: { left, top: 0, width: 10, height: 10 } as DOMRectReadOnly,
});
const hidden: ResolvedAnchor = { status: "hidden", reason: "display-none" };

function pushAnchor(anchorId: string, value: ResolvedAnchor) {
  act(() => { for (const listener of mockState.listeners.get(anchorId) ?? []) listener(value); });
}

describe("TutorialOverlay drag guides", () => {
  afterEach(() => {
    mockState.presentation = null;
    mockState.listeners.clear();
  });

  it("両anchorがreadyのときだけ tutorial-drag-guide を1件描画する", () => {
    mockState.presentation = {
      tutorialSessionId: "s1", revision: 1, challengeId: "c1", highlights: [],
      dragGuides: [{ guideId: "guide-1", fromAnchorId: "hotbar.hud", toAnchorId: "recipe.craft-button" }],
    };
    let renderer!: ReturnType<typeof create>;
    act(() => { renderer = create(createElement(TutorialOverlay)); });

    expect(renderer.root.findAllByProps({ "data-testid": "tutorial-drag-guide" }).length).toBe(0);

    pushAnchor("hotbar.hud", ready(10));
    expect(renderer.root.findAllByProps({ "data-testid": "tutorial-drag-guide" }).length).toBe(0);

    pushAnchor("recipe.craft-button", ready(100));
    const guides = renderer.root.findAllByProps({ "data-testid": "tutorial-drag-guide" });
    expect(guides.length).toBe(1);
    expect(guides[0].props.style.left).toBe(15);
  });

  it("片方のanchorが未解決に戻ると非表示になる", () => {
    mockState.presentation = {
      tutorialSessionId: "s1", revision: 1, challengeId: "c1", highlights: [],
      dragGuides: [{ guideId: "guide-1", fromAnchorId: "hotbar.hud", toAnchorId: "recipe.craft-button" }],
    };
    let renderer!: ReturnType<typeof create>;
    act(() => { renderer = create(createElement(TutorialOverlay)); });
    pushAnchor("hotbar.hud", ready(10));
    pushAnchor("recipe.craft-button", ready(100));
    expect(renderer.root.findAllByProps({ "data-testid": "tutorial-drag-guide" }).length).toBe(1);

    pushAnchor("recipe.craft-button", hidden);
    expect(renderer.root.findAllByProps({ "data-testid": "tutorial-drag-guide" }).length).toBe(0);
  });
});
