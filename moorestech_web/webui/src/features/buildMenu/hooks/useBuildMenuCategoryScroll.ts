import { useCallback, useEffect, useLayoutEffect, useRef, useState } from "react";
import {
  activeCategoryAtScroll,
  isJumpSettled,
  trailingSpacerHeight,
  type CategoryHeadingOffset,
} from "../logic/buildMenuScrollSpy";

type BuildMenuCategoryScroll = {
  activeCategoryGuid: string | null;
  spacerHeight: number;
  // 末尾スペーサが実測を1回終えたか。呼び出し側はこれを見てから復元代入する
  // Whether the trailing spacer has completed its first real measurement; callers gate restoration on this
  spacerMeasured: boolean;
  attachViewport: (viewport: HTMLDivElement | null) => void;
  headingRef: (categoryGuid: string) => (element: HTMLElement | null) => void;
  attachGroup: (categoryGuid: string) => (element: HTMLElement | null) => void;
  jumpTo: (categoryGuid: string) => void;
  handleScroll: (scrollTop: number) => void;
};

// ジャンプ中に固定を解除する操作イベント種別(D2案A)。userの意図的な入力のみを介入とみなす
// Interaction event kinds that release a jump pin (D2 option A); only intentional user input counts as intervention
const jumpInterventionEventTypes = ["wheel", "touchstart", "pointerdown", "keydown"] as const;

// DOM都合(視口・見出し位置・スムーズスクロール・末尾スペーサ)をここへ閉じ込め、判定は純関数へ委ねる
// Keeps DOM concerns (viewport, heading offsets, smooth scroll, trailing spacer) here and defers the math to pure functions
export function useBuildMenuCategoryScroll(visibleCategoryGuids: string[]): BuildMenuCategoryScroll {
  const viewportRef = useRef<HTMLDivElement | null>(null);
  const headingsRef = useRef(new Map<string, HTMLElement>());
  // カテゴリ群(section)ごとの要素。末尾群の高さはこの中から visibleCategoryGuids の末尾GUIDで引く
  // Per-category-group (section) elements; the trailing group's height is looked up here by the last visible guid
  const groupsRef = useRef(new Map<string, HTMLElement>());
  // ジャンプ中はハイライトを目標に固定し、到達か操作イベントでscroll-spyへ戻す(D2案A)
  // While jumping, pin the highlight to the target and release it to scroll-spy on arrival or an interaction event (D2 option A)
  const jumpTargetRef = useRef<{ categoryGuid: string; top: number } | null>(null);
  const [activeCategoryGuid, setActiveCategoryGuid] = useState<string | null>(null);
  const [spacerHeight, setSpacerHeight] = useState(0);
  const [spacerMeasured, setSpacerMeasured] = useState(false);
  // guid毎に安定したref callbackをここへキャッシュし、identity churnによるRO付け外しを防ぐ
  // Caches a stable ref callback per guid here so identity churn does not thrash the observer
  const headingRefCallbacksRef = useRef(new Map<string, (element: HTMLElement | null) => void>());
  const groupRefCallbacksRef = useRef(new Map<string, (element: HTMLElement | null) => void>());

  // viewportと各カテゴリ群(D1案C)の寸法変化を監視するObserver本体。要素の入れ替えはattach*で張り替える
  // Observer watching viewport and every category-group's dimensions (D1 option C); attach* callbacks re-target it when elements change
  const resizeObserverRef = useRef<ResizeObserver | null>(null);
  // Observerのコールバックは生成時ではなく呼び出し時にこのrefを経由するので、常に最新のレイアウト再判定処理を指す
  // The observer callback dereferences this ref at call time rather than construction time, so it always runs the latest layout re-judgment
  const layoutChangeRef = useRef<() => void>(() => {});

  // 呼び出し側の配列identityに依存せず、内容が変わった時だけeffectを走らせる
  // Key on contents so a caller's fresh array identity does not retrigger these effects
  const visibleKey = visibleCategoryGuids.join(",");

  // 見出しの上端は視口内容座標(offsetTop)で読む。viewportがoffsetParentになるようCSSで position:relative を与える
  // Heading tops are read in viewport content coordinates (offsetTop); CSS makes the viewport the offsetParent
  // ref と props だけを読むので、deps に含めなくても最新の値を返す
  // Reads only refs and props, so it stays current without being listed as a dep
  const headingOffsets = (): CategoryHeadingOffset[] =>
    visibleCategoryGuids
      .map((categoryGuid) => {
        const element = headingsRef.current.get(categoryGuid);
        return element ? { categoryGuid, top: element.offsetTop } : null;
      })
      .filter((offset): offset is CategoryHeadingOffset => offset !== null);

  const remeasureSpacer = (): void => {
    const viewport = viewportRef.current;
    if (viewport === null) return;
    const lastGuid = visibleCategoryGuids[visibleCategoryGuids.length - 1];
    const lastGroupHeight = lastGuid !== undefined ? groupsRef.current.get(lastGuid)?.offsetHeight ?? 0 : 0;
    setSpacerHeight(trailingSpacerHeight(viewport.clientHeight, lastGroupHeight));
    setSpacerMeasured(true);
  };

  const recomputeActiveCategory = (): void => {
    const viewport = viewportRef.current;
    if (viewport === null) return;
    setActiveCategoryGuid(activeCategoryAtScroll(headingOffsets(), viewport.scrollTop));
  };

  // ジャンプ固定中にレイアウトが動いた場合、目標座標を引き直して追いかける(D1案C)
  // While a jump is pinned and layout shifts, re-fetch the target coordinate and chase it (D1 option C)
  const retargetJump = (): void => {
    const viewport = viewportRef.current;
    const target = jumpTargetRef.current;
    if (viewport === null || target === null) return;
    const heading = headingsRef.current.get(target.categoryGuid);
    if (heading === undefined) return;
    const top = Math.min(heading.offsetTop, viewport.scrollHeight - viewport.clientHeight);
    if (top === target.top) return;
    jumpTargetRef.current = { categoryGuid: target.categoryGuid, top };
    if (isJumpSettled(viewport.scrollTop, top)) {
      jumpTargetRef.current = null;
      // クランプで到達先が動いた局面ではscroll-spy判定に任せず目標自身を確定させる
      // When clamping shifts the arrival point, settle on the target itself rather than deferring to scroll-spy
      setActiveCategoryGuid(target.categoryGuid);
      return;
    }
    viewport.scrollTo({ top, behavior: "smooth" });
  };

  // 寸法変化のたびに、末尾スペーサに加えジャンプ目標かハイライトのどちらかを測り直す(D1案C)
  // On every dimension change, remeasure the trailing spacer plus either the jump target or the highlight (D1 option C)
  const handleLayoutChange = (): void => {
    remeasureSpacer();
    if (jumpTargetRef.current !== null) retargetJump();
    else recomputeActiveCategory();
  };
  // レンダー本体で直接代入せず、コミット後のlayout effectでrefへ反映する
  // Assign in a post-commit layout effect rather than directly in the render body
  useLayoutEffect(() => {
    layoutChangeRef.current = handleLayoutChange;
  });

  // ResizeObserverはテスト環境等では未定義のことがあるため存在確認してから使う(visualViewport?.と同じ考え方)
  // ResizeObserver can be absent in some environments (e.g. tests), so check before use (same idea as visualViewport?.)
  const ensureResizeObserver = (): ResizeObserver | null => {
    if (typeof ResizeObserver === "undefined") return null;
    if (resizeObserverRef.current === null) {
      resizeObserverRef.current = new ResizeObserver(() => layoutChangeRef.current());
    }
    return resizeObserverRef.current;
  };

  useEffect(() => {
    return () => resizeObserverRef.current?.disconnect();
  }, []);

  const handleScroll = useCallback((scrollTop: number) => {
    const target = jumpTargetRef.current;
    if (target !== null) {
      if (isJumpSettled(scrollTop, target.top)) {
        jumpTargetRef.current = null;
        // クランプで到達先が動いた局面ではscroll-spy判定に任せず目標自身を確定させる
        // When clamping shifts the arrival point, settle on the target itself rather than deferring to scroll-spy
        setActiveCategoryGuid(target.categoryGuid);
      }
      // 未到達の間はscroll-spy判定を無視し、固定を保つ(解除は操作イベントかretargetJumpが担う)
      // While not settled, ignore scroll-spy resolution and keep the pin (release comes from an interaction event or retargetJump)
      return;
    }
    setActiveCategoryGuid(activeCategoryAtScroll(headingOffsets(), scrollTop));
  }, [visibleKey]);

  // ジャンプ固定を即座に解除しscroll-spyへ戻す。操作イベントの実処理はrefを経由し常に最新版を指す(D2案A)
  // Releases a jump pin immediately back to scroll-spy; the interaction handler goes through a ref so it always calls the latest version (D2 option A)
  const releaseJumpRef = useRef<() => void>(() => {
    if (jumpTargetRef.current === null) return;
    jumpTargetRef.current = null;
    recomputeActiveCategory();
  });

  // 表示群変化でスペーサ・現在地を再計算
  // Recomputes the trailing spacer and current category on visible-group change
  useLayoutEffect(() => {
    const viewport = viewportRef.current;
    if (viewport === null) return;
    remeasureSpacer();
    jumpTargetRef.current = null;
    setActiveCategoryGuid(activeCategoryAtScroll(headingOffsets(), viewport.scrollTop));
  }, [visibleKey]);

  const attachViewport = useCallback((viewport: HTMLDivElement | null) => {
    const observer = ensureResizeObserver();
    const previousViewport = viewportRef.current;
    if (previousViewport !== null) {
      observer?.unobserve(previousViewport);
      for (const type of jumpInterventionEventTypes) {
        previousViewport.removeEventListener(type, releaseJumpRef.current);
      }
    }
    viewportRef.current = viewport;
    if (viewport !== null) {
      observer?.observe(viewport);
      for (const type of jumpInterventionEventTypes) {
        viewport.addEventListener(type, releaseJumpRef.current, { passive: true });
      }
    }
  }, []);
  // guid毎の安定callbackを返す
  // Returns a stable callback per guid
  const headingRef = useCallback((categoryGuid: string) => {
    const cached = headingRefCallbacksRef.current.get(categoryGuid);
    if (cached !== undefined) return cached;
    const callback = (element: HTMLElement | null) => {
      if (element === null) headingsRef.current.delete(categoryGuid);
      else headingsRef.current.set(categoryGuid, element);
    };
    headingRefCallbacksRef.current.set(categoryGuid, callback);
    return callback;
  }, []);
  // guid毎に安定したcallbackで各カテゴリ群(section)をRO登録する(D1案C)。前例: shared/tutorialAnchor/anchorRegistry
  // A stable per-guid callback registers every category group (section) with the observer (D1 option C); precedent: shared/tutorialAnchor/anchorRegistry
  const attachGroup = useCallback((categoryGuid: string) => {
    const cached = groupRefCallbacksRef.current.get(categoryGuid);
    if (cached !== undefined) return cached;
    const callback = (element: HTMLElement | null) => {
      const observer = ensureResizeObserver();
      const previous = groupsRef.current.get(categoryGuid) ?? null;
      if (previous !== null) observer?.unobserve(previous);
      if (element === null) {
        groupsRef.current.delete(categoryGuid);
      } else {
        groupsRef.current.set(categoryGuid, element);
        observer?.observe(element);
      }
    };
    groupRefCallbacksRef.current.set(categoryGuid, callback);
    return callback;
  }, []);

  const jumpTo = useCallback((categoryGuid: string) => {
    const viewport = viewportRef.current;
    const heading = headingsRef.current.get(categoryGuid);
    if (viewport === null || heading === undefined) return;
    // 末尾スペーサ込みでも到達できない場合は最大スクロール位置を目標にして到達判定を成立させる
    // If even the spacer cannot reach the top, target the max scroll so the settle check can succeed
    const top = Math.min(heading.offsetTop, viewport.scrollHeight - viewport.clientHeight);
    jumpTargetRef.current = { categoryGuid, top };
    setActiveCategoryGuid(categoryGuid);
    if (isJumpSettled(viewport.scrollTop, top)) {
      jumpTargetRef.current = null;
      return;
    }
    viewport.scrollTo({ top, behavior: "smooth" });
  }, []);

  return { activeCategoryGuid, spacerHeight, spacerMeasured, attachViewport, headingRef, attachGroup, jumpTo, handleScroll };
}
