import { createElement } from "react";
import { act, create } from "react-test-renderer";
import { describe, expect, it, vi } from "vitest";
import { useBuildMenuCategoryScroll } from "./useBuildMenuCategoryScroll";

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

// スタブ疑似viewport要素
// A stubbed fake viewport element
function fakeViewport(overrides: Partial<{ clientHeight: number; scrollHeight: number; scrollTop: number }> = {}) {
  return {
    clientHeight: 600,
    scrollHeight: 1200,
    scrollTop: 0,
    scrollTo: vi.fn(),
    ...overrides,
  } as unknown as HTMLDivElement;
}

function fakeHeading(offsetTop: number) {
  return { offsetTop } as unknown as HTMLElement;
}

function fakeLastGroup(offsetHeight: number) {
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

describe("useBuildMenuCategoryScroll", () => {
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

  it("スムーズスクロール中にユーザーが介入し目標へ近づかなくなると固定が解除される", () => {
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
    // 200まで近づく: まだ固定
    // Closes to 200: still pinned
    act(() => latest.handleScroll(200));
    expect(latest.activeCategoryGuid).toBe("b");

    // 100まで離れ介入とみなす
    // Drifts to 100; treated as user intervention
    act(() => latest.handleScroll(100));
    expect(latest.activeCategoryGuid).toBe("a");
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
    const renderer = create(
      createElement(Harness, { visibleCategoryGuids: ["a"], onRender: (result) => { latest = result; } }),
    );
    const vp = fakeViewport({ clientHeight: 600 });
    act(() => {
      latest.attachViewport(vp);
      latest.attachLastGroup(fakeLastGroup(200));
    });

    act(() => {
      renderer.update(
        createElement(Harness, { visibleCategoryGuids: ["a", "b"], onRender: (result) => { latest = result; } }),
      );
    });
    expect(latest.spacerHeight).toBe(400);

    // 末尾群が視口以上で0クランプ
    // Clamps to 0 when the last group is at least as tall as the viewport
    act(() => {
      latest.attachLastGroup(fakeLastGroup(900));
      renderer.update(
        createElement(Harness, { visibleCategoryGuids: ["a"], onRender: (result) => { latest = result; } }),
      );
    });
    expect(latest.spacerHeight).toBe(0);
  });

  it("視口や末尾群のリサイズにspacerHeightがResizeObserver経由で追従する(カテゴリ集合は変わらない)", () => {
    const originalResizeObserver = globalThis.ResizeObserver;
    globalThis.ResizeObserver = FakeResizeObserver;
    FakeResizeObserver.instances = [];

    try {
      let latest!: HookResult;
      create(
        createElement(Harness, { visibleCategoryGuids: ["a"], onRender: (result) => { latest = result; } }),
      );
      const vp = fakeViewport({ clientHeight: 600 });
      const lastGroup = fakeLastGroup(200);
      act(() => {
        latest.attachViewport(vp);
        latest.attachLastGroup(lastGroup);
        for (const observer of FakeResizeObserver.instances) observer.fire();
      });
      expect(latest.spacerHeight).toBe(400);

      // 集合不変でリサイズのみ模す
      // Simulate only a resize, with visibleKey unchanged
      (vp as unknown as { clientHeight: number }).clientHeight = 900;
      act(() => {
        for (const observer of FakeResizeObserver.instances) observer.fire();
      });
      expect(latest.spacerHeight).toBe(700);

      // 末尾群の高さ変化も追従
      // A trailing-group height change tracks through the same path
      (lastGroup as unknown as { offsetHeight: number }).offsetHeight = 800;
      act(() => {
        for (const observer of FakeResizeObserver.instances) observer.fire();
      });
      expect(latest.spacerHeight).toBe(100);
    } finally {
      globalThis.ResizeObserver = originalResizeObserver;
    }
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
});
