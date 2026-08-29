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

// clientHeight/scrollHeight/scrollTop を保持し scrollTo をスタブした疑似viewport要素
// A fake viewport element holding clientHeight/scrollHeight/scrollTop and stubbing scrollTo
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
      latest.attachHeading("a", fakeHeading(0));
      latest.attachHeading("b", fakeHeading(400));
    });

    act(() => latest.jumpTo("b"));
    expect(vp.scrollTo).toHaveBeenCalledWith({ top: 400, behavior: "smooth" });
    expect(latest.activeCategoryGuid).toBe("b");

    // 目標(400)へ近づく途中(200)ではハイライトはbのまま固定される
    // While still closing in on the target (200), the highlight stays pinned to b
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
      latest.attachHeading("a", fakeHeading(0));
      latest.attachHeading("b", fakeHeading(400));
    });

    act(() => latest.jumpTo("b"));
    act(() => latest.handleScroll(400));
    expect(latest.activeCategoryGuid).toBe("b");

    // 固定解除後はscroll-spyが再開し、手スクロールでハイライトが変わる
    // After release, scroll-spy resumes and manual scrolling changes the highlight again
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
      latest.attachHeading("a", fakeHeading(0));
      latest.attachHeading("b", fakeHeading(400));
    });

    act(() => latest.jumpTo("b"));
    expect(vp.scrollTo).not.toHaveBeenCalled();

    // 固定は即座に解除されているので、手スクロールが即座にハイライトへ反映される
    // The pin was released immediately, so manual scrolling reflects on the highlight right away
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
      latest.attachHeading("a", fakeHeading(0));
      latest.attachHeading("b", fakeHeading(400));
    });

    act(() => latest.jumpTo("b"));
    // 200まで近づく(距離400→200): まだ固定
    // Closes to 200 (distance 400 -> 200): still pinned
    act(() => latest.handleScroll(200));
    expect(latest.activeCategoryGuid).toBe("b");

    // 100まで戻る(距離200→300): 目標から離れたのでユーザー介入とみなし固定解除
    // Drifts back to 100 (distance 200 -> 300): moved away, so treat it as user intervention and release
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
      latest.attachHeading("a", fakeHeading(0));
      latest.attachHeading("b", fakeHeading(400));
    });

    act(() => latest.jumpTo("b"));
    expect(latest.activeCategoryGuid).toBe("b");

    // 内容は同じだが参照が別の新規配列で再レンダー
    // Re-render with a fresh array instance that carries the same contents
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

    // 末尾群が視口以上に高ければ0にクランプされる
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
    // @ts-expect-error テスト用スタブでnode環境の欠落を埋める
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

      // カテゴリ集合(visibleKey)は変えずウィンドウリサイズだけを模す: 視口高が変わりResizeObserverが発火
      // Keep visibleKey unchanged and only simulate a window resize: viewport height changes and ResizeObserver fires
      (vp as unknown as { clientHeight: number }).clientHeight = 900;
      act(() => {
        for (const observer of FakeResizeObserver.instances) observer.fire();
      });
      expect(latest.spacerHeight).toBe(700);

      // 末尾カテゴリ群の高さ変化(ブループリント削除等)も同じ経路で追従する
      // A trailing-group height change (e.g. deleting a blueprint) tracks through the same path
      (lastGroup as unknown as { offsetHeight: number }).offsetHeight = 800;
      act(() => {
        for (const observer of FakeResizeObserver.instances) observer.fire();
      });
      expect(latest.spacerHeight).toBe(100);
    } finally {
      globalThis.ResizeObserver = originalResizeObserver;
    }
  });
});
