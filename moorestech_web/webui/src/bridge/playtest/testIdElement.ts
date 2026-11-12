import { ancestorClipRect } from "../../shared/tutorialAnchor/ancestorClip";

// 一致件数と先頭要素を返す。重複の扱いは呼び出し側が決める
// Return the match count and first element; the caller decides how to handle duplicates
export function findByTestId(testid: string): { element: HTMLElement | null; matchCount: number } {
  const matches = document.querySelectorAll<HTMLElement>(`[data-testid="${CSS.escape(testid)}"]`);
  return { element: matches.length === 0 ? null : matches[0], matchCount: matches.length };
}

// 真偽のdata属性だけを状態フラグとして返す
// Return only boolean-like data attributes as state flags
export function readStateFlags(element: HTMLElement): string[] {
  const flags: string[] = [];
  for (const attribute of Array.from(element.attributes)) {
    if (!attribute.name.startsWith("data-") || attribute.name === "data-testid") continue;
    if (attribute.value === "" || attribute.value === "true") flags.push(attribute.name.slice("data-".length));
  }
  return flags.sort();
}

export function isElementVisible(element: HTMLElement, rect: DOMRect): boolean {
  if (element.hidden || element.closest('[aria-hidden="true"]')) return false;
  const style = getComputedStyle(element);
  if (style.display === "none" || style.visibility === "hidden") return false;

  // 祖先のクリップと画面に実際に見える面積を要求する
  // Require visible area within both ancestor clips and the viewport
  if (rect.width <= 0 || rect.height <= 0) return false;
  const clip = ancestorClipRect(element);
  return Math.max(rect.left, clip.left) < Math.min(rect.right, clip.right) &&
    Math.max(rect.top, clip.top) < Math.min(rect.bottom, clip.bottom);
}
