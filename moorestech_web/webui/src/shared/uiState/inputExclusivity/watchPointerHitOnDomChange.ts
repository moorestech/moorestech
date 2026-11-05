import { isPointerOverWebUi } from "../activeLayer";

// 静止したポインタの下でDOMが変わるたび、その位置の当たり判定をフレーム単位で通知する
// Re-hit-test a stationary pointer position whenever the DOM changes, at most once per frame
// 休止解除後はtopicの再取得を待って描かれるHUDがあり、pointermove無しでは占有が伝わらないため
// After resume some HUDs render only once their topic arrives, so without a pointermove the occupancy would never reach Unity
export function watchPointerHitOnDomChange(x: number, y: number, onHit: (pointerOverUi: boolean) => void): () => void {
  let frame: number | null = null;
  const observer = new MutationObserver(() => {
    if (frame != null) return;
    frame = requestAnimationFrame(() => {
      frame = null;
      onHit(isPointerOverWebUi(document.elementFromPoint(x, y)));
    });
  });
  // 要素の追加と表示切替（class・style・hidden）の両方で描画が変わる
  // Both element insertion and visibility toggles (class/style/hidden) change what is under the pointer
  observer.observe(document.body, { childList: true, subtree: true, attributes: true, attributeFilter: ["class", "style", "hidden"] });
  return () => {
    observer.disconnect();
    if (frame != null) cancelAnimationFrame(frame);
  };
}
