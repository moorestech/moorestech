import { resolveTutorialAnchor, type ResolvedAnchor } from "./resolveAnchor";
import { tutorialAnchorSelector } from "./tutorialAnchor";

// 追従(位置更新)はrAFへ集約するが、撤去(アンカーのDOM離脱)だけは同期で流す
// Tracking (position updates) coalesces into rAF, while teardown (an anchor leaving the DOM) is pushed synchronously
export class TutorialAnchorRegistry {
  private readonly listeners = new Map<string, Set<(value: ResolvedAnchor) => void>>();
  // 直近の解決で見えていた要素。離脱判定に使うだけなので矩形もスタイルも持たない
  // Elements seen at the last resolve; references only, so detecting departure needs no layout read
  private readonly tracked = new Map<string, HTMLElement[]>();
  private readonly mutation = new MutationObserver(() => this.onMutation());
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
      if (set.size !== 0) return;
      this.listeners.delete(anchorId);
      this.tracked.delete(anchorId);
    };
  }

  dispose() {
    this.mutation.disconnect();
    this.resize.disconnect();
    this.intersection.disconnect();
    cancelAnimationFrame(this.frame);
    this.tracked.clear();
    document.removeEventListener("scroll", this.markAllDirty, true);
    window.removeEventListener("resize", this.markAllDirty);
    visualViewport?.removeEventListener("resize", this.markAllDirty);
    visualViewport?.removeEventListener("scroll", this.markAllDirty);
  }

  // 撤去をrAFに載せるとアンカー無き枠線が次の描画まで残る。CEFは外部BeginFrame駆動で自力の描画を起こせず、その空きがゴーストの寿命になる
  // Deferring teardown to rAF leaves an anchorless outline until the next paint; CEF cannot self-schedule one, so that gap becomes the ghost's lifetime
  private readonly onMutation = () => {
    for (const [anchorId, listeners] of this.listeners) {
      const elements = this.tracked.get(anchorId);
      if (!elements || elements.length === 0) continue;
      if (elements.every((element) => element.isConnected)) continue;
      // ノードの差し替えは撤去ではないので次の解決へ委ね、真の消滅だけを即座に落とす
      // A node swap is not a teardown, so leave it to the next resolve and drop only a true disappearance
      if (document.querySelector(tutorialAnchorSelector(anchorId)) !== null) continue;
      this.tracked.set(anchorId, []);
      for (const listener of listeners) listener({ status: "not-found", reason: "missing" });
    }
    this.markAllDirty();
  };

  private readonly markAllDirty = () => {
    if (this.frame !== 0) return;
    this.frame = requestAnimationFrame(() => {
      this.frame = 0;
      this.resize.disconnect();
      this.intersection.disconnect();
      for (const [anchorId, listeners] of this.listeners) {
        const elements = [...document.querySelectorAll<HTMLElement>(tutorialAnchorSelector(anchorId))];
        for (const element of elements) {
          this.resize.observe(element);
          this.intersection.observe(element);
        }
        this.tracked.set(anchorId, elements);
        const value = resolveTutorialAnchor(anchorId);
        for (const listener of listeners) listener(value);
      }
    });
  };
}
