// ドラッグガイド矢印の中核ゲート（両anchor ready時のみ描画）を検証する
// Verifies the drag-guide's core gate: it renders only while both anchors are ready
import { createElement } from "react";
import { act, create } from "react-test-renderer";
import { afterEach, describe, expect, it, vi } from "vitest";
import type { ResolvedAnchor } from "@/shared/tutorialAnchor";
import { dispatchAction } from "@/bridge";
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

vi.mock("@/shared/i18n", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/shared/i18n")>();
  return { ...actual, useI18n: () => ({ t: (key: string) => `T:${key}` }) };
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

const outline = (elementId: string, anchorId: string) => ({
  kind: "outline" as const, elementId, anchorId, paddingPx: 0, blocksPointerInput: false,
});
const dragGuide = (elementId: string, fromAnchorId: string, toAnchorId: string) => ({
  kind: "dragGuide" as const, elementId, fromAnchorId, toAnchorId,
});
const presentation = (revision: number, sessions: TutorialPresentationData["sessions"]) => ({ revision, sessions });

describe("TutorialOverlay drag guides", () => {
  afterEach(() => {
    mockState.presentation = null;
    mockState.listeners.clear();
  });

  it("両anchorがreadyのときだけ tutorial-drag-guide を1件描画する", () => {
    mockState.presentation = presentation(1, [
      { tutorialSessionId: "s1", challengeId: "c1", elements: [dragGuide("guide-1", "hotbar.hud", "recipe.craft-button")] },
    ]);
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
    mockState.presentation = presentation(1, [
      { tutorialSessionId: "s1", challengeId: "c1", elements: [dragGuide("guide-1", "hotbar.hud", "recipe.craft-button")] },
    ]);
    let renderer!: ReturnType<typeof create>;
    act(() => { renderer = create(createElement(TutorialOverlay)); });
    pushAnchor("hotbar.hud", ready(10));
    pushAnchor("recipe.craft-button", ready(100));
    expect(renderer.root.findAllByProps({ "data-testid": "tutorial-drag-guide" }).length).toBe(1);

    pushAnchor("recipe.craft-button", hidden);
    expect(renderer.root.findAllByProps({ "data-testid": "tutorial-drag-guide" }).length).toBe(0);
  });
});

describe("TutorialOverlay anchor resolution", () => {
  afterEach(() => {
    mockState.presentation = null;
    mockState.listeners.clear();
    vi.mocked(dispatchAction).mockClear();
  });

  // 同一anchorを指す複数highlightが全てackされる（先着1件で潰さない）
  // Every highlight pointing at the same anchor is acked, not just the first one found
  it("同一anchorを共有する全highlightへackを配る", () => {
    mockState.presentation = presentation(4, [
      { tutorialSessionId: "s1", challengeId: "c1", elements: [outline("highlight-1", "recipe.craft-button")] },
      { tutorialSessionId: "s2", challengeId: "c2", elements: [outline("highlight-2", "recipe.craft-button")] },
    ]);
    act(() => { create(createElement(TutorialOverlay)); });

    pushAnchor("recipe.craft-button", ready(10));

    const acked = vi.mocked(dispatchAction).mock.calls.map(([, payload]) => payload);
    expect(acked).toEqual([
      { tutorialSessionId: "s1", revision: 4, elementId: "highlight-1", anchorId: "recipe.craft-button",
        status: "ready", reason: "mounted" },
      { tutorialSessionId: "s2", revision: 4, elementId: "highlight-2", anchorId: "recipe.craft-button",
        status: "ready", reason: "mounted" },
    ]);
  });

  // revision更新で解決済みanchorを全消去せず、表示中の要素を消灯させない
  // A revision bump keeps already-resolved anchors so visible elements do not blink off
  it("revision更新でも購読が続くanchorの解決状態を保つ", () => {
    mockState.presentation = presentation(1, [
      { tutorialSessionId: "s1", challengeId: "c1", elements: [outline("highlight-1", "recipe.craft-button")] },
    ]);
    let renderer!: ReturnType<typeof create>;
    act(() => { renderer = create(createElement(TutorialOverlay)); });
    pushAnchor("recipe.craft-button", ready(10));
    expect(renderer.root.findAllByProps({ "data-kind": "outline" }).length).toBe(1);

    mockState.presentation = presentation(2, [
      { tutorialSessionId: "s1", challengeId: "c1", elements: [
        outline("highlight-1", "recipe.craft-button"), outline("highlight-2", "hotbar.hud"),
      ] },
    ]);
    act(() => { renderer.update(createElement(TutorialOverlay)); });

    expect(renderer.root.findAllByProps({ "data-kind": "outline" }).length).toBe(1);
    expect(renderer.root.findAllByProps({ "data-kind": "outline" })[0].props.style.left).toBe(10);
  });

  // 購読が切れたanchorの解決状態は落とす
  // Anchors that left the subscription set are dropped
  it("購読対象外になったanchorの解決状態は落とす", () => {
    mockState.presentation = presentation(1, [
      { tutorialSessionId: "s1", challengeId: "c1", elements: [outline("highlight-1", "recipe.craft-button")] },
    ]);
    let renderer!: ReturnType<typeof create>;
    act(() => { renderer = create(createElement(TutorialOverlay)); });
    pushAnchor("recipe.craft-button", ready(10));

    mockState.presentation = presentation(2, [
      { tutorialSessionId: "s1", challengeId: "c1", elements: [outline("highlight-1", "hotbar.hud")] },
    ]);
    act(() => { renderer.update(createElement(TutorialOverlay)); });

    expect(renderer.root.findAllByProps({ "data-kind": "outline" }).length).toBe(0);
  });

  // 同値のrectが再通知されても再描画しない（参照等価では常にfalseになる死んだ分岐だった）
  // Re-notifying an equal-valued rect must not re-render; reference equality made that branch dead
  it("同値rectの再解決では再描画しない", () => {
    mockState.presentation = presentation(1, [
      { tutorialSessionId: "s1", challengeId: "c1", elements: [outline("highlight-1", "recipe.craft-button")] },
    ]);
    let renderer!: ReturnType<typeof create>;
    act(() => { renderer = create(createElement(TutorialOverlay)); });
    pushAnchor("recipe.craft-button", ready(10));
    const styleBefore = renderer.root.findByProps({ "data-kind": "outline" }).props.style;

    pushAnchor("recipe.craft-button", ready(10));

    expect(renderer.root.findByProps({ "data-kind": "outline" }).props.style).toBe(styleBefore);

    pushAnchor("recipe.craft-button", ready(20));
    expect(renderer.root.findByProps({ "data-kind": "outline" }).props.style).not.toBe(styleBefore);
  });
});

describe("TutorialOverlay outline labels", () => {
  afterEach(() => { mockState.presentation = null; mockState.listeners.clear(); });

  // labelTutorialGuid付きの枠線だけが文言ラベルを持ち、文言は challengeTutorial.<guid>.text で解決する
  // Only outlines with labelTutorialGuid get a text label, resolved through challengeTutorial.<guid>.text
  it("labelTutorialGuid がある枠線だけラベルを描く", () => {
    mockState.presentation = presentation(1, [
      { tutorialSessionId: "s1", challengeId: "c1", elements: [
        { ...outline("h1", "recipe.craft-button"), labelTutorialGuid: "11111111-1111-4111-8111-111111111111" },
        outline("h2", "hotbar.hud"),
      ] },
    ]);
    let renderer!: ReturnType<typeof create>;
    act(() => { renderer = create(createElement(TutorialOverlay)); });
    pushAnchor("recipe.craft-button", ready(10));
    pushAnchor("hotbar.hud", ready(100));

    const labels = renderer.root.findAllByProps({ "data-testid": "tutorial-highlight-label" });
    expect(labels.length).toBe(1);
    expect(labels[0].children).toEqual(["T:challengeTutorial.11111111-1111-4111-8111-111111111111.text"]);
    // ラベルは枠線の下辺外側（top = rect.bottom + padding）に置く
    // The label sits just below the outline (top = rect.bottom + padding)
    expect(labels[0].props.style.top).toBe(10);
    expect(labels[0].props.style.left).toBe(10);
  });

  it("anchor未解決の枠線はラベルも描かない", () => {
    mockState.presentation = presentation(1, [
      { tutorialSessionId: "s1", challengeId: "c1", elements: [
        { ...outline("h1", "recipe.craft-button"), labelTutorialGuid: "11111111-1111-4111-8111-111111111111" },
      ] },
    ]);
    let renderer!: ReturnType<typeof create>;
    act(() => { renderer = create(createElement(TutorialOverlay)); });
    pushAnchor("recipe.craft-button", hidden);
    expect(renderer.root.findAllByProps({ "data-testid": "tutorial-highlight-label" }).length).toBe(0);
  });
});
