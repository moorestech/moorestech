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

// vitestはnode環境でdocumentを持たないため、CSS変数読み取りをテスト用の固定値へ差し替える
// vitest runs in a node environment with no document, so the CSS variable read is swapped for a fixed test value
vi.mock("./highlightGlowToken", () => ({
  readTutorialHighlightGlowPx: () => 4,
}));

import { TutorialOverlay } from "./TutorialOverlay";

const FULL_CLIP = { left: -100, top: -100, right: 1280, bottom: 820 };
const ready = (left: number, clip = FULL_CLIP): ResolvedAnchor => ({
  status: "ready", reason: "mounted",
  rect: { left, top: 0, width: 10, height: 10 } as DOMRectReadOnly,
  clip,
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
const keyControl = (elementId: string) => ({
  kind: "keyControl" as const, elementId, tutorialGuid: "22222222-2222-4222-8222-222222222222",
  keyName: "Tab", uiState: "GameScreen",
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

  // labelGuid時のみラベル描画
  // Label renders only when labelGuid is set
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
    // top=枠線下辺+padding
    // top = outline bottom + padding
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

describe("TutorialOverlay keyControl exclusion", () => {
  afterEach(() => { mockState.presentation = null; mockState.listeners.clear(); });

  // keyControl混在でも件数不変
  // Count is unchanged when keyControl is mixed in
  it("keyControlが混在してもoutline描画件数は変わらない", () => {
    mockState.presentation = presentation(1, [
      { tutorialSessionId: "s1", challengeId: "c1", elements: [
        outline("h1", "recipe.craft-button"), keyControl("k1"),
      ] },
    ]);
    let renderer!: ReturnType<typeof create>;
    act(() => { renderer = create(createElement(TutorialOverlay)); });
    pushAnchor("recipe.craft-button", ready(10));

    expect(renderer.root.findAllByProps({ "data-kind": "outline" }).length).toBe(1);
    expect(renderer.root.findAllByProps({ "data-testid": "key-control-hint" }).length).toBe(0);
  });
});

describe("TutorialOverlay outline clipping", () => {
  afterEach(() => {
    mockState.presentation = null;
    mockState.listeners.clear();
  });

  it("祖先クリップの外へ出た辺はclip-pathで切られる", () => {
    mockState.presentation = presentation(1, [
      { tutorialSessionId: "s1", challengeId: "c1", elements: [outline("highlight-1", "research.node-a")] },
    ]);
    let renderer!: ReturnType<typeof create>;
    act(() => { renderer = create(createElement(TutorialOverlay)); });

    // rectは left:100 top:0 の 10x10、paddingPx:0 なのでboxは 100..110 / 0..10。右辺だけがclipに掛かる
    // The rect is 10x10 at left:100 top:0 with paddingPx:0 so the box spans 100..110 / 0..10; only the right side clips
    pushAnchor("research.node-a", ready(100, { left: -100, top: -100, right: 104, bottom: 820 }));

    const outlines = renderer.root.findAllByProps({ "data-kind": "outline" });
    expect(outlines.length).toBe(1);
    expect(outlines[0].props.style.clipPath).toBe("inset(-4px 6px -4px -4px)");
  });

  it("完全にクリップされた枠は要素ごと描かない", () => {
    mockState.presentation = presentation(1, [
      { tutorialSessionId: "s1", challengeId: "c1", elements: [outline("highlight-1", "research.node-a")] },
    ]);
    let renderer!: ReturnType<typeof create>;
    act(() => { renderer = create(createElement(TutorialOverlay)); });

    pushAnchor("research.node-a", ready(100, { left: -100, top: -100, right: 50, bottom: 820 }));

    expect(renderer.root.findAllByProps({ "data-kind": "outline" }).length).toBe(0);
  });

  // clip.rightがboxのすぐ外（グロー幅未満）にあるとクランプ済みinset値では非交差を見逃す
  // clip.right just outside the box (within the glow width) must not slip past the clamped-inset check
  it("グロー幅未満だけ外れた枠は要素ごと描かない", () => {
    mockState.presentation = presentation(1, [
      { tutorialSessionId: "s1", challengeId: "c1", elements: [outline("highlight-1", "research.node-a")] },
    ]);
    let renderer!: ReturnType<typeof create>;
    act(() => { renderer = create(createElement(TutorialOverlay)); });

    // boxは100..110。clip.rightが99なので実際は完全にclipの外だが、-4pxクランプ後の和では素通りしうる
    // box spans 100..110; clip.right=99 puts it fully outside, but the -4px-clamped sum could let it through
    pushAnchor("research.node-a", ready(100, { left: -100, top: -100, right: 99, bottom: 820 }));

    expect(renderer.root.findAllByProps({ "data-kind": "outline" }).length).toBe(0);
  });

  // clipだけが変わった場合（コンテナのリサイズ等）に再描画されないとマスクが古いまま残る
  // A clip-only change (container resize etc.) must still re-render, or the mask goes stale
  it("rectが同値でもclipが変われば再描画する", () => {
    mockState.presentation = presentation(1, [
      { tutorialSessionId: "s1", challengeId: "c1", elements: [outline("highlight-1", "research.node-a")] },
    ]);
    let renderer!: ReturnType<typeof create>;
    act(() => { renderer = create(createElement(TutorialOverlay)); });

    // クリップが遠い＝どの辺も切らない。グロー4pxを残すため全辺 -4px になる
    // A far-away clip cuts no side, so every side is -4px to preserve the 4px glow
    pushAnchor("research.node-a", ready(100, { left: -100, top: -100, right: 1280, bottom: 820 }));
    expect(renderer.root.findAllByProps({ "data-kind": "outline" })[0].props.style.clipPath)
      .toBe("inset(-4px -4px -4px -4px)");

    pushAnchor("research.node-a", ready(100, { left: -100, top: -100, right: 104, bottom: 820 }));
    expect(renderer.root.findAllByProps({ "data-kind": "outline" })[0].props.style.clipPath)
      .toBe("inset(-4px 6px -4px -4px)");
  });
});
