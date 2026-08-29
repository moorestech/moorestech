import { useCallback, useEffect, useLayoutEffect, useRef, useState } from "react";
import {
  activeCategoryAtScroll,
  isJumpAbandoned,
  isJumpSettled,
  trailingSpacerHeight,
  type CategoryHeadingOffset,
} from "../logic/buildMenuScrollSpy";

type BuildMenuCategoryScroll = {
  activeCategoryGuid: string | null;
  spacerHeight: number;
  attachViewport: (viewport: HTMLDivElement | null) => void;
  attachHeading: (categoryGuid: string, element: HTMLElement | null) => void;
  attachLastGroup: (element: HTMLElement | null) => void;
  jumpTo: (categoryGuid: string) => void;
  handleScroll: (scrollTop: number) => void;
};

// DOM都合(視口・見出し位置・スムーズスクロール・末尾スペーサ)をここへ閉じ込め、判定は純関数へ委ねる
// Keeps DOM concerns (viewport, heading offsets, smooth scroll, trailing spacer) here and defers the math to pure functions
export function useBuildMenuCategoryScroll(visibleCategoryGuids: string[]): BuildMenuCategoryScroll {
  const viewportRef = useRef<HTMLDivElement | null>(null);
  const headingsRef = useRef(new Map<string, HTMLElement>());
  const lastGroupRef = useRef<HTMLElement | null>(null);
  // ジャンプ中はハイライトを目標に固定し、到達か介入でscroll-spyへ戻す
  // While jumping, pin the highlight to the target and release it to scroll-spy on arrival or intervention
  const jumpTargetRef = useRef<{ categoryGuid: string; top: number; previousScrollTop: number } | null>(null);
  const [activeCategoryGuid, setActiveCategoryGuid] = useState<string | null>(null);
  const [spacerHeight, setSpacerHeight] = useState(0);

  // viewportと末尾カテゴリ群の寸法変化を監視するObserver本体。要素の入れ替えはattach*で張り替える
  // Observer watching viewport and trailing-group dimensions; attach* callbacks re-target it when elements change
  const resizeObserverRef = useRef<ResizeObserver | null>(null);
  // Observerのコールバックは生成時ではなく呼び出し時にこのrefを経由するので、常に最新の測り直し処理を指す
  // The observer callback dereferences this ref at call time rather than construction time, so it always runs the latest remeasure logic
  const remeasureSpacerRef = useRef<() => void>(() => {});

  const remeasureSpacer = (): void => {
    const viewport = viewportRef.current;
    if (viewport === null) return;
    const lastGroupHeight = lastGroupRef.current?.offsetHeight ?? 0;
    setSpacerHeight(trailingSpacerHeight(viewport.clientHeight, lastGroupHeight));
  };
  remeasureSpacerRef.current = remeasureSpacer;

  // ResizeObserverはテスト環境等では未定義のことがあるため存在確認してから使う(visualViewport?.と同じ考え方)
  // ResizeObserver can be absent in some environments (e.g. tests), so check before use (same idea as visualViewport?.)
  const ensureResizeObserver = (): ResizeObserver | null => {
    if (typeof ResizeObserver === "undefined") return null;
    if (resizeObserverRef.current === null) {
      resizeObserverRef.current = new ResizeObserver(() => remeasureSpacerRef.current());
    }
    return resizeObserverRef.current;
  };

  useEffect(() => {
    return () => resizeObserverRef.current?.disconnect();
  }, []);

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

  const handleScroll = useCallback((scrollTop: number) => {
    const target = jumpTargetRef.current;
    if (target !== null) {
      if (isJumpSettled(scrollTop, target.top)) {
        jumpTargetRef.current = null;
      } else if (isJumpAbandoned(target.previousScrollTop, scrollTop, target.top)) {
        // 目標へ近づかなくなった＝ユーザーの介入なので固定を解除しscroll-spyへ戻す
        // Distance stopped shrinking, meaning the user intervened; release the pin back to scroll-spy
        jumpTargetRef.current = null;
      } else {
        jumpTargetRef.current = { ...target, previousScrollTop: scrollTop };
        return;
      }
    }
    setActiveCategoryGuid(activeCategoryAtScroll(headingOffsets(), scrollTop));
  }, [visibleKey]);

  // 表示群が変わったら末尾スペーサと現在地を取り直す
  // Recompute the trailing spacer and current category whenever the visible groups change
  useLayoutEffect(() => {
    const viewport = viewportRef.current;
    if (viewport === null) return;
    remeasureSpacer();
    jumpTargetRef.current = null;
    setActiveCategoryGuid(activeCategoryAtScroll(headingOffsets(), viewport.scrollTop));
  }, [visibleKey]);

  const attachViewport = useCallback((viewport: HTMLDivElement | null) => {
    const observer = ensureResizeObserver();
    if (viewportRef.current !== null) observer?.unobserve(viewportRef.current);
    viewportRef.current = viewport;
    if (viewport !== null) observer?.observe(viewport);
  }, []);
  const attachHeading = useCallback((categoryGuid: string, element: HTMLElement | null) => {
    if (element === null) headingsRef.current.delete(categoryGuid);
    else headingsRef.current.set(categoryGuid, element);
  }, []);
  const attachLastGroup = useCallback((element: HTMLElement | null) => {
    const observer = ensureResizeObserver();
    if (lastGroupRef.current !== null) observer?.unobserve(lastGroupRef.current);
    lastGroupRef.current = element;
    if (element !== null) observer?.observe(element);
  }, []);

  const jumpTo = useCallback((categoryGuid: string) => {
    const viewport = viewportRef.current;
    const heading = headingsRef.current.get(categoryGuid);
    if (viewport === null || heading === undefined) return;
    // 末尾スペーサ込みでも到達できない場合は最大スクロール位置を目標にして到達判定を成立させる
    // If even the spacer cannot reach the top, target the max scroll so the settle check can succeed
    const top = Math.min(heading.offsetTop, viewport.scrollHeight - viewport.clientHeight);
    jumpTargetRef.current = { categoryGuid, top, previousScrollTop: viewport.scrollTop };
    setActiveCategoryGuid(categoryGuid);
    if (isJumpSettled(viewport.scrollTop, top)) {
      jumpTargetRef.current = null;
      return;
    }
    viewport.scrollTo({ top, behavior: "smooth" });
  }, []);

  return { activeCategoryGuid, spacerHeight, attachViewport, attachHeading, attachLastGroup, jumpTo, handleScroll };
}
