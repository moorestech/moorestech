import { resolveTutorialAnchor, type ResolvedAnchor } from "./resolveAnchor";

export class TutorialAnchorRegistry {
  private readonly listeners = new Map<string, Set<(value: ResolvedAnchor) => void>>();
  private readonly mutation = new MutationObserver(() => this.markAllDirty());
  private readonly resize = new ResizeObserver(() => this.markAllDirty());
  private readonly intersection = new IntersectionObserver(() => this.markAllDirty());
  private frame = 0;

  constructor() {
    this.mutation.observe(document.body, { childList: true, subtree: true, attributes: true });
    document.addEventListener("scroll", this.markAllDirty, true);
    window.addEventListener("resize", this.markAllDirty);
    visualViewport?.addEventListener("resize", this.markAllDirty);
    visualViewport?.addEventListener("scroll", this.markAllDirty);
  }

  subscribe(anchorId: string, listener: (value: ResolvedAnchor) => void) {
    const set = this.listeners.get(anchorId) ?? new Set();
    set.add(listener);
    this.listeners.set(anchorId, set);
    this.markAllDirty();
    return () => {
      set.delete(listener);
      if (set.size === 0) this.listeners.delete(anchorId);
    };
  }

  dispose() {
    this.mutation.disconnect();
    this.resize.disconnect();
    this.intersection.disconnect();
    cancelAnimationFrame(this.frame);
    document.removeEventListener("scroll", this.markAllDirty, true);
    window.removeEventListener("resize", this.markAllDirty);
    visualViewport?.removeEventListener("resize", this.markAllDirty);
    visualViewport?.removeEventListener("scroll", this.markAllDirty);
  }

  private readonly markAllDirty = () => {
    if (this.frame !== 0) return;
    this.frame = requestAnimationFrame(() => {
      this.frame = 0;
      this.resize.disconnect();
      this.intersection.disconnect();
      for (const [anchorId, listeners] of this.listeners) {
        const escaped = globalThis.CSS?.escape ? globalThis.CSS.escape(anchorId) : anchorId.replaceAll('"', '\\"');
        for (const element of document.querySelectorAll<HTMLElement>(`[data-tutorial-anchor="${escaped}"]`)) {
          this.resize.observe(element);
          this.intersection.observe(element);
        }
        const value = resolveTutorialAnchor(anchorId);
        for (const listener of listeners) listener(value);
      }
    });
  };
}
