import { useEffect, type Dispatch, type RefObject, type SetStateAction } from "react";
import { shiftViewportForResize, type ViewportSize, type ViewportTransform } from "./viewport";
import { toContentBox } from "./viewportElement";

// ビューポートの寸法が変わっても画面中央に見えていた点を中央に保つ
// 初回の中央寄せの直後に見出し等の遅延描画でビューポートが縮むと、寄せた先が中央からずれるため
// Keeps whatever was at the view center at the center when the viewport is resized
// A late-rendered heading can shrink the viewport right after the one-shot centering, which would otherwise push the focus off center
export function useKeepViewCenterOnResize(
  viewportElement: RefObject<HTMLDivElement | null>,
  setViewport: Dispatch<SetStateAction<ViewportTransform>>,
  enabled: boolean,
) {
  useEffect(() => {
    // 保存済み視点の復元マウントは復元値が最終寸法の座標なので動かさない
    // A mount restored from a stored view keeps it as is, since the stored value is already in final-size coordinates
    if (!enabled) return;
    const element = viewportElement.current;
    // silent-ok: ResizeObserver が無いのは jsdom 等のテスト環境だけで、CEF・ブラウザには常にある / 寸法変化の追従は e2e（challenge.spec の中央寄せ）が検出する
    if (!element || typeof ResizeObserver === "undefined") return;
    let lastSize: ViewportSize = sizeOf(element);
    const observer = new ResizeObserver(() => {
      const nextSize = sizeOf(element);
      if (nextSize.width === lastSize.width && nextSize.height === lastSize.height) return;
      const previousSize = lastSize;
      lastSize = nextSize;
      // 寸法0からの変化は初回レイアウトで、TreeView の中央寄せ見送りと対称に平行移動しない
      // A change from a zero size is the first layout; skip the shift, symmetric with TreeView's deferred centering
      if (previousSize.width === 0 || previousSize.height === 0) return;
      setViewport((current) => shiftViewportForResize(current, previousSize, nextSize));
    });
    observer.observe(element);
    return () => observer.disconnect();
  }, [viewportElement, setViewport, enabled]);
}

function sizeOf(element: HTMLDivElement): ViewportSize {
  const contentBox = toContentBox(element);
  return { width: contentBox.width, height: contentBox.height };
}
