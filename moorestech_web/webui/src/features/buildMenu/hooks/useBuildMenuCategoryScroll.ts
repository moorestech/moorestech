import { useCallback, useLayoutEffect, useRef, useState } from "react";
import {
  activeCategoryAtScroll,
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
  // ジャンプ中はハイライトを目標に固定し、到達でscroll-spyへ戻す
  // While jumping, pin the highlight to the target and release it to scroll-spy on arrival
  const jumpTargetRef = useRef<{ categoryGuid: string; top: number } | null>(null);
  const [activeCategoryGuid, setActiveCategoryGuid] = useState<string | null>(null);
  const [spacerHeight, setSpacerHeight] = useState(0);

  // 見出しの上端は視口内容座標(offsetTop)で読む。viewportがoffsetParentになるようCSSで position:relative を与える
  // Heading tops are read in viewport content coordinates (offsetTop); CSS makes the viewport the offsetParent
  const headingOffsets = (): CategoryHeadingOffset[] =>
    visibleCategoryGuids
      .map((categoryGuid) => {
        const element = headingsRef.current.get(categoryGuid);
        return element ? { categoryGuid, top: element.offsetTop } : null;
      })
      .filter((offset): offset is CategoryHeadingOffset => offset !== null);

  const spy = useCallback((scrollTop: number) => {
    const target = jumpTargetRef.current;
    if (target !== null) {
      if (!isJumpSettled(scrollTop, target.top)) return;
      jumpTargetRef.current = null;
    }
    setActiveCategoryGuid(activeCategoryAtScroll(headingOffsets(), scrollTop));
  // headingOffsets は ref と props だけを読む
  // headingOffsets reads only refs and props
  }, [visibleCategoryGuids]);

  // 表示群が変わったら末尾スペーサと現在地を取り直す
  // Recompute the trailing spacer and current category whenever the visible groups change
  useLayoutEffect(() => {
    const viewport = viewportRef.current;
    if (viewport === null) return;
    const lastGroupHeight = lastGroupRef.current?.offsetHeight ?? 0;
    setSpacerHeight(trailingSpacerHeight(viewport.clientHeight, lastGroupHeight));
    jumpTargetRef.current = null;
    setActiveCategoryGuid(activeCategoryAtScroll(headingOffsets(), viewport.scrollTop));
  }, [visibleCategoryGuids]);

  const attachViewport = useCallback((viewport: HTMLDivElement | null) => {
    viewportRef.current = viewport;
  }, []);
  const attachHeading = useCallback((categoryGuid: string, element: HTMLElement | null) => {
    if (element === null) headingsRef.current.delete(categoryGuid);
    else headingsRef.current.set(categoryGuid, element);
  }, []);
  const attachLastGroup = useCallback((element: HTMLElement | null) => {
    lastGroupRef.current = element;
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

  return { activeCategoryGuid, spacerHeight, attachViewport, attachHeading, attachLastGroup, jumpTo, handleScroll: spy };
}
