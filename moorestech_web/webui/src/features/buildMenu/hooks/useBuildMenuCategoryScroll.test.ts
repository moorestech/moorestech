import { createElement } from "react";
import { act, create } from "react-test-renderer";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { useBuildMenuCategoryScroll } from "./useBuildMenuCategoryScroll";
import { loadBuildMenuSessionState, updateBuildMenuSessionState } from "../sessionState/buildMenuSessionState";

type HookResult = ReturnType<typeof useBuildMenuCategoryScroll>;

// フックの戻り値をレンダー毎にテストへ引き渡すだけのハーネス
// A harness that only hands the hook's return value out to the test on every render
function Harness({
  visibleCategoryGuids,
  onRender,
}: {
  visibleCategoryGuids: string[];
  onRender: (result: HookResult) => void;
}) {
  const result = useBuildMenuCategoryScroll(visibleCategoryGuids);
  onRender(result);
  return null;
}

// スタブ疑似viewport要素。addEventListener/removeEventListenerは手動発火できるよう記録する
// A stubbed fake viewport element; addEventListener/removeEventListener are recorded so a test can fire them manually
function fakeViewport(overrides: Partial<{ clientHeight: number; scrollHeight: number; scrollTop: number }> = {}) {
  const listeners = new Map<string, Set<EventListener>>();
  return {
    clientHeight: 600,
    scrollHeight: 1200,
    scrollTop: 0,
    scrollTo: vi.fn(),
    addEventListener: vi.fn((type: string, listener: EventListener) => {
      const set = listeners.get(type) ?? new Set();
      set.add(listener);
      listeners.set(type, set);
    }),
    removeEventListener: vi.fn((type: string, listener: EventListener) => {
      listeners.get(type)?.delete(listener);
    }),
    dispatch(type: string) {
      for (const listener of listeners.get(type) ?? []) listener({} as Event);
    },
    ...overrides,
  } as unknown as HTMLDivElement & { dispatch: (type: string) => void };
}

function fakeHeading(offsetTop: number) {
  return { offsetTop } as unknown as HTMLElement;
}

function fakeGroup(offsetHeight: number) {
  return { offsetHeight } as unknown as HTMLElement;
}

// テスト環境(node)にはResizeObserverが無いためのスタブ。observeされた要素とコールバックを保持し、手動で発火できる
// Stub for the ResizeObserver missing in the node test environment; keeps observed elements/callback so a test can fire it manually
class FakeResizeObserver {
  static instances: FakeResizeObserver[] = [];
  readonly callback: ResizeObserverCallback;
  readonly observed = new Set<unknown>();

  constructor(callback: ResizeObserverCallback) {
    this.callback = callback;
    FakeResizeObserver.instances.push(this);
  }

  observe(target: unknown) {
    this.observed.add(target);
  }

  unobserve(target: unknown) {
    this.observed.delete(target);
  }

  disconnect() {
    this.observed.clear();
  }

  fire() {
    this.callback([] as unknown as ResizeObserverEntry[], this as unknown as ResizeObserver);
  }
}

function withFakeResizeObserver(run: () => void) {
  const originalResizeObserver = globalThis.ResizeObserver;
  globalThis.ResizeObserver = FakeResizeObserver;
  FakeResizeObserver.instances = [];
  try {
    run();
  } finally {
    globalThis.ResizeObserver = originalResizeObserver;
  }
}

describe("useBuildMenuCategoryScroll", () => {
  // 復元・保存はフックが担うため、保存位置をテスト間で持ち越さない
  // The hook owns restore and save, so the saved position must not carry across tests
  beforeEach(() => {
    updateBuildMenuSessionState({ scrollTop: 0 });
  });

  it("jumpTo後、目標未到達のscrollではハイライトが動かない", () => {
    let latest!: HookResult;
    create(
      createElement(Harness, { visibleCategoryGuids: ["a", "b"], onRender: (result) => { latest = result; } }),
    );
    const vp = fakeViewport();
    act(() => {
      latest.attachViewport(vp);
      latest.headingRef("a")(fakeHeading(0));
      latest.headingRef("b")(fakeHeading(400));
    });

    act(() => latest.jumpTo("b"));
    expect(vp.scrollTo).toHaveBeenCalledWith({ top: 400, behavior: "smooth" });
    expect(latest.activeCategoryGuid).toBe("b");

    // 距離200では固定のまま
    // Still pinned at distance 200
    act(() => latest.handleScroll(200));
    expect(latest.activeCategoryGuid).toBe("b");
  });

  it("目標到達scrollで固定が解除されscroll-spyへ戻る", () => {
    let latest!: HookResult;
    create(
      createElement(Harness, { visibleCategoryGuids: ["a", "b"], onRender: (result) => { latest = result; } }),
    );
    const vp = fakeViewport();
    act(() => {
      latest.attachViewport(vp);
      latest.headingRef("a")(fakeHeading(0));
      latest.headingRef("b")(fakeHeading(400));
    });

    act(() => latest.jumpTo("b"));
    act(() => latest.handleScroll(400));
    expect(latest.activeCategoryGuid).toBe("b");

    // 解除後は手スクロールに追従
    // After release, tracks manual scrolling
    act(() => latest.handleScroll(0));
    expect(latest.activeCategoryGuid).toBe("a");
  });

  it("既に目標位置ならジャンプ固定が即座に解除される", () => {
    let latest!: HookResult;
    create(
      createElement(Harness, { visibleCategoryGuids: ["a", "b"], onRender: (result) => { latest = result; } }),
    );
    const vp = fakeViewport({ scrollTop: 400 });
    act(() => {
      latest.attachViewport(vp);
      latest.headingRef("a")(fakeHeading(0));
      latest.headingRef("b")(fakeHeading(400));
    });

    act(() => latest.jumpTo("b"));
    expect(vp.scrollTo).not.toHaveBeenCalled();

    // 即解除、手スクロール即反映
    // Released immediately; manual scroll reflects right away
    act(() => latest.handleScroll(0));
    expect(latest.activeCategoryGuid).toBe("a");
  });

  it("ジャンプ中にviewportへwheelを送ると、目標未到達でも固定が解除されscroll-spyへ戻る(D2案A)", () => {
    let latest!: HookResult;
    create(
      createElement(Harness, { visibleCategoryGuids: ["a", "b"], onRender: (result) => { latest = result; } }),
    );
    const vp = fakeViewport();
    act(() => {
      latest.attachViewport(vp);
      latest.headingRef("a")(fakeHeading(0));
      latest.headingRef("b")(fakeHeading(400));
    });

    act(() => latest.jumpTo("b"));
    expect(latest.activeCategoryGuid).toBe("b");

    // 目標(400)には遠く及ばない位置で操作イベントが発火しても即解除
    // Even far from the target (400), the interaction event releases the pin immediately
    act(() => {
      vp.scrollTop = 50;
      vp.dispatch("wheel");
    });
    expect(latest.activeCategoryGuid).toBe("a");

    // 解除後はscroll-spyがそのまま追従する
    // After release, scroll-spy tracking resumes normally
    act(() => latest.handleScroll(400));
    expect(latest.activeCategoryGuid).toBe("b");
  });

  it("visibleCategoryGuidsが同内容の別配列で再レンダーされてもジャンプ固定が維持される", () => {
    let latest!: HookResult;
    const renderer = create(
      createElement(Harness, { visibleCategoryGuids: ["a", "b"], onRender: (result) => { latest = result; } }),
    );
    const vp = fakeViewport();
    act(() => {
      latest.attachViewport(vp);
      latest.headingRef("a")(fakeHeading(0));
      latest.headingRef("b")(fakeHeading(400));
    });

    act(() => latest.jumpTo("b"));
    expect(latest.activeCategoryGuid).toBe("b");

    // 同内容・別配列で再レンダー
    // Re-render with the same contents in a new array
    act(() => {
      renderer.update(
        createElement(Harness, { visibleCategoryGuids: ["a", "b"], onRender: (result) => { latest = result; } }),
      );
    });
    expect(latest.activeCategoryGuid).toBe("b");
  });

  it("spacerHeightは視口高から末尾群高を引いた値になる(0クランプ込み)", () => {
    let latest!: HookResult;
    // 末尾GUIDは常に"a"に固定し、先頭側だけ入れ替えてvisibleKeyを動かし直す
    // Keep the trailing guid fixed at "a" and vary only the leading one to nudge visibleKey
    const renderer = create(
      createElement(Harness, { visibleCategoryGuids: ["z", "a"], onRender: (result) => { latest = result; } }),
    );
    const vp = fakeViewport({ clientHeight: 600 });
    act(() => {
      latest.attachViewport(vp);
      latest.attachGroup("a")(fakeGroup(200));
      renderer.update(
        createElement(Harness, { visibleCategoryGuids: ["y", "a"], onRender: (result) => { latest = result; } }),
      );
    });
    expect(latest.spacerHeight).toBe(400);

    // 末尾群が視口以上で0クランプ
    // Clamps to 0 when the last group is at least as tall as the viewport
    act(() => {
      latest.attachGroup("a")(fakeGroup(900));
      renderer.update(
        createElement(Harness, { visibleCategoryGuids: ["x", "a"], onRender: (result) => { latest = result; } }),
      );
    });
    expect(latest.spacerHeight).toBe(0);
  });

  it("上方カテゴリのエントリが減って見出し位置がずれると、ハイライトがResizeObserver経由で追従する(D1案C回帰)", () => {
    withFakeResizeObserver(() => {
      let latest!: HookResult;
      create(
        createElement(Harness, { visibleCategoryGuids: ["a", "b"], onRender: (result) => { latest = result; } }),
      );
      // 視口の初期位置は保存位置の復元で決まるため、両者を揃えて開き直し直後を模す
      // The viewport's initial position comes from restoring the saved one, so keep both in sync to model a reopen
      updateBuildMenuSessionState({ scrollTop: 350 });
      const vp = fakeViewport({ scrollTop: 350 });
      const headingA = fakeHeading(0);
      const headingB = fakeHeading(400);
      const groupA = fakeGroup(400);
      act(() => {
        latest.attachViewport(vp);
        latest.headingRef("a")(headingA);
        latest.headingRef("b")(headingB);
        latest.attachGroup("a")(groupA);
        latest.attachGroup("b")(fakeGroup(200));
        for (const observer of FakeResizeObserver.instances) observer.fire();
      });
      // scrollTop 350はb(400)未満なのでまだa
      // scrollTop 350 is still below b(400), so it's still a
      expect(latest.activeCategoryGuid).toBe("a");

      // 上方カテゴリaのエントリが減り、bの見出しが300まで上がる。GUID集合もclientHeight/lastGroupも不変
      // Category a shrinks and b's heading rises to 300; the guid set and clientHeight/lastGroup stay unchanged
      (headingB as unknown as { offsetTop: number }).offsetTop = 300;
      (groupA as unknown as { offsetHeight: number }).offsetHeight = 300;
      act(() => {
        for (const observer of FakeResizeObserver.instances) observer.fire();
      });
      expect(latest.activeCategoryGuid).toBe("b");
    });
  });

  it("視口や末尾群のリサイズにspacerHeightがResizeObserver経由で追従する(カテゴリ集合は変わらない)", () => {
    withFakeResizeObserver(() => {
      let latest!: HookResult;
      create(
        createElement(Harness, { visibleCategoryGuids: ["a"], onRender: (result) => { latest = result; } }),
      );
      const vp = fakeViewport({ clientHeight: 600 });
      const group = fakeGroup(200);
      act(() => {
        latest.attachViewport(vp);
        latest.attachGroup("a")(group);
        for (const observer of FakeResizeObserver.instances) observer.fire();
      });
      expect(latest.spacerHeight).toBe(400);

      // 集合不変でウィンドウ(viewport)高のリサイズのみ模す
      // Simulate only a viewport-height resize, with the guid set unchanged
      (vp as unknown as { clientHeight: number }).clientHeight = 900;
      act(() => {
        for (const observer of FakeResizeObserver.instances) observer.fire();
      });
      expect(latest.spacerHeight).toBe(700);

      // 末尾群の高さ変化も追従
      // A trailing-group height change tracks through the same path
      (group as unknown as { offsetHeight: number }).offsetHeight = 800;
      act(() => {
        for (const observer of FakeResizeObserver.instances) observer.fire();
      });
      expect(latest.spacerHeight).toBe(100);
    });
  });

  it("末尾付近でクランプが効いても到達後は目標カテゴリがハイライトされる", () => {
    let latest!: HookResult;
    create(
      createElement(Harness, { visibleCategoryGuids: ["a", "b"], onRender: (result) => { latest = result; } }),
    );
    // scrollHeight(500) < clientHeight(600)のため最大スクロール位置がマイナスへクランプされる
    // scrollHeight(500) < clientHeight(600), so the max scroll position clamps below zero
    const vp = fakeViewport({ clientHeight: 600, scrollHeight: 500 });
    act(() => {
      latest.attachViewport(vp);
      latest.headingRef("a")(fakeHeading(0));
      latest.headingRef("b")(fakeHeading(400));
    });

    act(() => latest.jumpTo("b"));
    expect(vp.scrollTo).toHaveBeenCalledWith({ top: -100, behavior: "smooth" });

    // クランプされた目標(-100)へ到達した時点で、activeCategoryAtScrollの再計算ではなく目標自身が確定する
    // On arriving at the clamped target (-100), settle on the target itself rather than re-deriving via activeCategoryAtScroll
    act(() => latest.handleScroll(-100));
    expect(latest.activeCategoryGuid).toBe("b");
  });

  it("開いた直後、scrollイベントを一度も発火せずに初期ハイライトが視口位置から決まる", () => {
    let latest!: HookResult;
    const renderer = create(
      createElement(Harness, { visibleCategoryGuids: ["a"], onRender: (result) => { latest = result; } }),
    );
    const vp = fakeViewport({ scrollTop: 400 });
    act(() => {
      latest.attachViewport(vp);
      latest.headingRef("a")(fakeHeading(0));
      latest.headingRef("b")(fakeHeading(400));
    });

    // scroll無しで初期ハイライト確定
    // Settles the initial highlight via layout effect alone, without calling handleScroll
    act(() => {
      renderer.update(
        createElement(Harness, { visibleCategoryGuids: ["a", "b"], onRender: (result) => { latest = result; } }),
      );
    });
    expect(latest.activeCategoryGuid).toBe("b");
  });
  it("末尾群が未マウントの間は復元も保存位置の上書きもしない", () => {
    updateBuildMenuSessionState({ scrollTop: 250 });
    let latest!: HookResult;
    const renderer = create(
      createElement(Harness, { visibleCategoryGuids: ["z", "a"], onRender: (result) => { latest = result; } }),
    );
    const vp = fakeViewport({ clientHeight: 600 });
    act(() => { latest.attachViewport(vp); });

    // 末尾群が無い＝スペーサ実測不能。ここで実測済み扱いにすると保存位置がクランプで0へ潰れる
    // No trailing group means the spacer is unmeasurable; treating it as measured clamps the saved position to 0
    expect(latest.spacerHeight).toBe(0);
    expect(vp.scrollTop).toBe(0);
    expect(loadBuildMenuSessionState().scrollTop).toBe(250);

    act(() => {
      latest.attachGroup("a")(fakeGroup(200));
      renderer.update(
        createElement(Harness, { visibleCategoryGuids: ["y", "a"], onRender: (result) => { latest = result; } }),
      );
    });
    // 実測が済んだ時点でフック自身が保存位置へ戻す
    // Once measured, the hook itself restores the saved position
    expect(latest.spacerHeight).toBe(400);
    expect(vp.scrollTop).toBe(250);
    expect(loadBuildMenuSessionState().scrollTop).toBe(250);
  });

  it("表示群が入れ替わった後のwheel解除でも最新の見出し集合で現在地を測り直す", () => {
    let latest!: HookResult;
    const renderer = create(
      createElement(Harness, { visibleCategoryGuids: ["a"], onRender: (result) => { latest = result; } }),
    );
    const vp = fakeViewport();
    act(() => {
      latest.attachViewport(vp);
      latest.headingRef("a")(fakeHeading(0));
    });
    act(() => {
      renderer.update(
        createElement(Harness, { visibleCategoryGuids: ["a", "b"], onRender: (result) => { latest = result; } }),
      );
      latest.headingRef("b")(fakeHeading(400));
    });

    act(() => latest.jumpTo("b"));
    act(() => {
      vp.scrollTop = 400;
      vp.dispatch("wheel");
    });
    // 初期クロージャを購読へ焼くと見出し集合が["a"]のままで、必ず"a"へ落ちる
    // Freezing the first closure into the subscription keeps the heading set at ["a"] and always falls back to "a"
    expect(latest.activeCategoryGuid).toBe("b");
  });

  it("末尾ジャンプ中にスペーサが伸びたら、反映後の高さで目標を引き直す", () => {
    withFakeResizeObserver(() => {
      updateBuildMenuSessionState({ scrollTop: 0 });
      let latest!: HookResult;
      create(
        createElement(Harness, { visibleCategoryGuids: ["a", "b"], onRender: (result) => { latest = result; } }),
      );
      const vp = fakeViewport({ clientHeight: 600 });
      // 内容高700に確定済みスペーサを足したものがscrollHeight。latestはコミット済みの値なのでDOM反映を模せる
      // scrollHeight is the 700px content plus the committed spacer; latest holds the committed value, so it models the DOM
      Object.defineProperty(vp, "scrollHeight", { get: () => 700 + latest.spacerHeight });
      act(() => {
        latest.attachViewport(vp);
        latest.headingRef("a")(fakeHeading(0));
        latest.headingRef("b")(fakeHeading(300));
      });

      // スペーサ未実測では最大スクロールが100しかなく、目標はクランプされる
      // Without the spacer the max scroll is only 100, so the target is clamped
      act(() => latest.jumpTo("b"));
      expect(vp.scrollTo).toHaveBeenLastCalledWith({ top: 100, behavior: "smooth" });

      act(() => {
        latest.attachGroup("b")(fakeGroup(100));
        for (const observer of FakeResizeObserver.instances) observer.fire();
      });
      // 反映前のscrollHeightで引き直すと100のまま到達扱いになり、見出しは視口上端へ来ない
      // Re-targeting on the pre-commit scrollHeight keeps 100 and calls it arrived, leaving the heading short of the top
      expect(latest.spacerHeight).toBe(500);
      expect(vp.scrollTo).toHaveBeenLastCalledWith({ top: 300, behavior: "smooth" });
    });
  });
});
