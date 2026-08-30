import { useCallback, useEffect, useLayoutEffect, useRef, useState } from "react";
import {
  activeCategoryAtScroll,
  isJumpSettled,
  trailingSpacerHeight,
  type CategoryHeadingOffset,
} from "../logic/buildMenuScrollSpy";
import { loadBuildMenuSessionState, updateBuildMenuSessionState } from "../sessionState/buildMenuSessionState";

type BuildMenuCategoryScroll = {
  activeCategoryGuid: string | null;
  spacerHeight: number;
  attachViewport: (viewport: HTMLDivElement | null) => void;
  headingRef: (categoryGuid: string) => (element: HTMLElement | null) => void;
  attachGroup: (categoryGuid: string) => (element: HTMLElement | null) => void;
  jumpTo: (categoryGuid: string) => void;
  handleScroll: (scrollTop: number) => void;
};

// ジャンプ中に固定を解除する操作イベント種別(D2案A)。userの意図的な入力のみを介入とみなす
// Interaction event kinds that release a jump pin (D2 option A); only intentional user input counts as intervention
const jumpInterventionEventTypes = ["wheel", "touchstart", "pointerdown", "keydown"] as const;

// DOM都合(視口・見出し位置・スムーズスクロール・末尾スペーサ・位置復元)をここへ閉じ込め、判定は純関数へ委ねる
// Keeps DOM concerns (viewport, heading offsets, smooth scroll, trailing spacer, position restore) here and defers the math to pure functions
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
  // 視口を張り替えた回数。復元・実測をやり直す契機として使う
  // Counts viewport swaps; used as the trigger to redo measurement and restoration
  const [viewportGeneration, setViewportGeneration] = useState(0);
  // スペーサ反映後にジャンプ目標を引き直す要求回数
  // Number of requests to re-target the jump once the spacer has landed
  const [jumpRetargetRequests, setJumpRetargetRequests] = useState(0);
  // 保存スクロール位置を代入済みか。視口を張り替えたら偽へ戻す
  // Whether the saved scroll position has been applied; reset when the viewport is swapped
  const scrollRestoredRef = useRef(false);
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
  // 解除処理も同型にrefを経由する。初期クロージャを購読へ焼くと古いカテゴリ集合で判定し続ける
  // The release path goes through a ref for the same reason; freezing the first closure into the subscription judges on a stale guid set forever
  const releaseJumpRef = useRef<() => void>(() => {});
  // 購読の付け外しでidentityを一致させるため、登録するのは常にこのラッパ1個
  // Always registers this single wrapper so add/removeEventListener see the same identity
  const releaseJumpListenerRef = useRef<() => void>(() => releaseJumpRef.current());

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

  // 末尾群がまだDOMに無い間は実測不能。高さ0の実測値として確定させると復元位置まで0へ潰れる
  // While the trailing group is absent from the DOM the spacer is unmeasurable; settling it as a real height of 0 flattens the restore target to 0 too
  const desiredSpacerHeight = (): number | null => {
    const viewport = viewportRef.current;
    if (viewport === null) return null;
    const lastGuid = visibleCategoryGuids[visibleCategoryGuids.length - 1];
    if (lastGuid === undefined) return null;
    const lastGroup = groupsRef.current.get(lastGuid);
    if (lastGroup === undefined) return null;
    return trailingSpacerHeight(viewport.clientHeight, lastGroup.offsetHeight);
  };

  const remeasureSpacer = (): void => {
    const desired = desiredSpacerHeight();
    if (desired === null) return;
    setSpacerHeight(desired);
  };

  const recomputeActiveCategory = (): void => {
    const viewport = viewportRef.current;
    if (viewport === null) return;
    setActiveCategoryGuid(activeCategoryAtScroll(headingOffsets(), viewport.scrollTop));
  };

  // 保存位置は末尾スペーサ確定後に1回だけ代入する。確定前だとクランプされた値でストアを潰す
  // Applies the saved position exactly once, after the trailing spacer settles; earlier assignment corrupts the store with a clamped value
  const restoreScrollOnce = (viewport: HTMLDivElement): void => {
    if (scrollRestoredRef.current) return;
    scrollRestoredRef.current = true;
    viewport.scrollTop = loadBuildMenuSessionState().scrollTop;
    // クランプ後の実効値へ揃え直し、ハイライトも復元先で決め直す
    // Realigns the store with the clamped effective value and re-derives the highlight at the restored position
    updateBuildMenuSessionState({ scrollTop: viewport.scrollTop });
    recomputeActiveCategory();
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

  // ジャンプ固定を即座に解除しscroll-spyへ戻す(D2案A)
  // Releases a jump pin immediately back to scroll-spy (D2 option A)
  const releaseJump = (): void => {
    if (jumpTargetRef.current === null) return;
    jumpTargetRef.current = null;
    recomputeActiveCategory();
  };

  // 寸法変化のたびに、末尾スペーサに加えジャンプ目標かハイライトのどちらかを測り直す(D1案C)
  // On every dimension change, remeasure the trailing spacer plus either the jump target or the highlight (D1 option C)
  const handleLayoutChange = (): void => {
    remeasureSpacer();
    // 反映前のscrollHeightで引き直すと末尾ジャンプが手前で到達扱いになるため、追従はコミット後のeffectへ回す
    // Re-targeting on the pre-commit scrollHeight settles a trailing jump short of the heading, so chasing waits for a post-commit effect
    if (jumpTargetRef.current !== null) setJumpRetargetRequests((requests) => requests + 1);
    else recomputeActiveCategory();
  };
  // レンダー本体で直接代入せず、コミット後のlayout effectでrefへ反映する
  // Assign in a post-commit layout effect rather than directly in the render body
  useLayoutEffect(() => {
    layoutChangeRef.current = handleLayoutChange;
    releaseJumpRef.current = releaseJump;
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
    // 位置の保存もこのフックが担う。パネル側と二重に持つと視口の所在が割れる
    // Saving the position is this hook's job too; splitting it with the panel would fork ownership of the viewport
    updateBuildMenuSessionState({ scrollTop });
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

  // 表示群が変わったらジャンプ固定を捨て、現在地を測り直す
  // Drops the jump pin and re-derives the current category whenever the visible groups change
  useLayoutEffect(() => {
    const viewport = viewportRef.current;
    if (viewport === null) return;
    jumpTargetRef.current = null;
    setActiveCategoryGuid(activeCategoryAtScroll(headingOffsets(), viewport.scrollTop));
  }, [visibleKey]);

  // 表示群・視口の変化でスペーサを測り直し、反映が済んだ視口へ保存位置を復元する
  // Remeasures the spacer on a visible-group or viewport change and restores the saved position into the settled viewport
  useLayoutEffect(() => {
    const viewport = viewportRef.current;
    if (viewport === null) return;
    const desired = desiredSpacerHeight();
    if (desired === null) return;
    if (desired !== spacerHeight) {
      setSpacerHeight(desired);
      return;
    }
    restoreScrollOnce(viewport);
  }, [visibleKey, viewportGeneration, spacerHeight]);

  // スペーサがDOMへ反映された後の高さで目標を引き直す
  // Re-targets using the height the DOM carries after the spacer has landed
  useLayoutEffect(() => {
    retargetJump();
  }, [jumpRetargetRequests]);

  // アンマウント時のscrollイベントは次フレームまで合体され間に合わないため、DOM除去前の実効値を確定保存する
  // Scroll events coalesce until the next frame and miss the unmount, so persist the effective value before DOM removal
  useLayoutEffect(() => () => {
    const viewport = viewportRef.current;
    if (viewport === null) return;
    updateBuildMenuSessionState({ scrollTop: viewport.scrollTop });
  }, []);

  const attachViewport = useCallback((viewport: HTMLDivElement | null) => {
    const previousViewport = viewportRef.current;
    if (viewport === previousViewport) return;
    const observer = ensureResizeObserver();
    if (previousViewport !== null) {
      // 張り替え前の実効位置を確定保存し、新しい視口へ引き継ぐ
      // Persists the outgoing viewport's effective position so the incoming one inherits it
      updateBuildMenuSessionState({ scrollTop: previousViewport.scrollTop });
      observer?.unobserve(previousViewport);
      for (const type of jumpInterventionEventTypes) {
        previousViewport.removeEventListener(type, releaseJumpListenerRef.current);
      }
    }
    viewportRef.current = viewport;
    if (viewport !== null) {
      observer?.observe(viewport);
      for (const type of jumpInterventionEventTypes) {
        viewport.addEventListener(type, releaseJumpListenerRef.current, { passive: true });
      }
    }
    // 新しい視口はscrollTop 0から始まるので復元をやり直す
    // A fresh viewport starts at scrollTop 0, so restoration has to run again
    scrollRestoredRef.current = false;
    setViewportGeneration((generation) => generation + 1);
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

  return { activeCategoryGuid, spacerHeight, attachViewport, headingRef, attachGroup, jumpTo, handleScroll };
}
