// アンカーの祖先を辿り、実際にクリップを掛けている要素のpadding boxを交差させる
// Walk the anchor's ancestors, intersecting the padding box of every element that truly clips it
//
// CSSの規則: 祖先Aのoverflowは、Aが子孫Dの包含ブロック連鎖の内側にある場合にのみDをクリップする
// CSS rule: ancestor A's overflow clips descendant D only while A sits inside D's containing-block chain
//
// position:fixed なアンカーは実在する(crosshair・trainHud)が、いずれも.stage(transform)の直下で即捕捉される
// Fixed-position anchors do exist (crosshair, trainHud) but are captured immediately by .stage's transform
// そのため脱出則は毎フレーム評価されるがマスク結果は変わらない。スクロールコンテナ内へ
// So the escape branch runs every frame without changing the mask outcome. Only once a fixed modal
// position:fixed のモーダルを置いた場合に結果が変わり、誤るとハイライトが丸ごと消える
// lands inside a scroller does the result change, and a mistake there hides the highlight entirely
// 変更する場合は docs/adr/0023-tutorial-highlight-ancestor-clip-mask.md の Consequences を読むこと
// Read the Consequences section of ADR 0023 before changing them

export type ClipRect = { left: number; top: number; right: number; bottom: number };

export function ancestorClipRect(element: HTMLElement): ClipRect {
  let clip: ClipRect = { left: 0, top: 0, right: innerWidth, bottom: innerHeight };
  let escape = getComputedStyle(element).position;
  let node = element.parentElement;
  while (node) {
    const style = getComputedStyle(node);
    const containsFixed = createsFixedContainingBlock(style);
    const containsAbsolute = containsFixed || style.position !== "static";

    // この祖先が脱出力を捕まえるか
    // Whether this ancestor captures the escape
    let clipsHere = true;
    if (escape === "fixed") {
      clipsHere = containsFixed;
      if (containsFixed) escape = "static";
    } else if (escape === "absolute") {
      clipsHere = containsAbsolute;
      if (containsAbsolute) escape = "static";
    }
    if (clipsHere) {
      if (clipsContent(style)) clip = intersect(clip, paddingBox(node, style));

      // 捕捉された祖先自身のpositionが、これより上での脱出力になる
      // The captured ancestor's own position becomes the escape in effect above it
      if (style.position === "fixed") escape = "fixed";
      else if (style.position === "absolute") escape = "absolute";
    }
    node = node.parentElement;
  }
  return clip;
}

// clip適用後のinset値。nullは非描画
// The inset value after applying clip; null means the caller skips rendering
//
// clip-pathの参照ボックスはborder boxなので inset(0px) でも box-shadow の外側グローが切り落とされる
// clip-path resolves against the border box, so even inset(0px) shaves off the box-shadow's outer glow
// 非クリップ辺は-outsetPxで装飾温存
// Non-clipped sides use -outsetPx to preserve the decoration
export function clipPathInset(box: ClipRect, clip: ClipRect, outsetPx: number): string | null {
  if (isDisjoint(box, clip)) return null;
  const top = Math.max(-outsetPx, clip.top - box.top);
  const right = Math.max(-outsetPx, box.right - clip.right);
  const bottom = Math.max(-outsetPx, box.bottom - clip.bottom);
  const left = Math.max(-outsetPx, clip.left - box.left);
  if (top + bottom >= box.bottom - box.top || left + right >= box.right - box.left) return null;
  return `inset(${top}px ${right}px ${bottom}px ${left}px)`;
}

// クランプ済みinset値での空判定はグロー分の帯を見逃すため、素の矩形で先に非交差を判定する
// The clamped inset values can miss a fully-clipped box, so check raw rect disjointness first
function isDisjoint(box: ClipRect, clip: ClipRect): boolean {
  return clip.right <= box.left || clip.left >= box.right ||
    clip.bottom <= box.top || clip.top >= box.bottom;
}

function createsFixedContainingBlock(style: CSSStyleDeclaration) {
  return style.transform !== "none" || style.filter !== "none" || style.perspective !== "none" ||
    style.backdropFilter !== "none" || style.willChange.includes("transform") ||
    style.willChange.includes("filter") || style.willChange.includes("perspective") ||
    style.contain.includes("paint") || style.contain.includes("strict") || style.contain === "content";
}

function clipsContent(style: CSSStyleDeclaration) {
  return style.overflow !== "visible" || style.clipPath !== "none" ||
    style.contain.includes("paint") || style.contain.includes("strict") || style.contain === "content";
}

// overflowのクリップ境界はborder boxではなくpadding box
// The overflow clip edge is the padding box, not the border box
function paddingBox(node: HTMLElement, style: CSSStyleDeclaration): ClipRect {
  const box = node.getBoundingClientRect();
  return {
    left: box.left + (parseFloat(style.borderLeftWidth) || 0),
    top: box.top + (parseFloat(style.borderTopWidth) || 0),
    right: box.right - (parseFloat(style.borderRightWidth) || 0),
    bottom: box.bottom - (parseFloat(style.borderBottomWidth) || 0),
  };
}

function intersect(a: ClipRect, b: ClipRect): ClipRect {
  return { left: Math.max(a.left, b.left), top: Math.max(a.top, b.top),
    right: Math.min(a.right, b.right), bottom: Math.min(a.bottom, b.bottom) };
}
