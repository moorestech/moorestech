export type AnchorReason = "mounted" | "missing" | "duplicate-anchor" | "display-none" |
  "visibility-hidden" | "aria-hidden" | "zero-area" | "outside-viewport";
export type ResolvedAnchor =
  | { status: "ready"; reason: "mounted"; rect: DOMRectReadOnly }
  | { status: "not-found"; reason: "missing" | "duplicate-anchor" }
  | { status: "hidden"; reason: Exclude<AnchorReason, "mounted" | "missing" | "duplicate-anchor"> };

export function resolveTutorialAnchor(anchorId: string): ResolvedAnchor {
  const escaped = globalThis.CSS?.escape ? globalThis.CSS.escape(anchorId) : anchorId.replaceAll('"', '\\"');
  const selector = `[data-tutorial-anchor="${escaped}"]`;
  const matches = document.querySelectorAll<HTMLElement>(selector);
  if (matches.length === 0) return { status: "not-found", reason: "missing" };
  if (matches.length > 1) return { status: "not-found", reason: "duplicate-anchor" };
  const element = matches[0];
  if (element.hidden || element.closest('[aria-hidden="true"]')) return { status: "hidden", reason: "aria-hidden" };
  const style = getComputedStyle(element);
  if (style.display === "none") return { status: "hidden", reason: "display-none" };
  if (style.visibility === "hidden") return { status: "hidden", reason: "visibility-hidden" };
  const rect = element.getBoundingClientRect();
  if (rect.width <= 0 || rect.height <= 0) return { status: "hidden", reason: "zero-area" };
  if (rect.bottom <= 0 || rect.right <= 0 || rect.top >= innerHeight || rect.left >= innerWidth) {
    return { status: "hidden", reason: "outside-viewport" };
  }
  return { status: "ready", reason: "mounted", rect };
}
