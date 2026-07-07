import { useLayoutEffect, useState } from "react";
import { ItemSlot } from "@/shared/ui";
import type { SlotData } from "@/bridge/contract/payloadTypes";
import styles from "./GrabOverlay.module.css";

// 掴み開始座標の供給源。grab は必ず mousedown で始まるため mousedown のみ常時追跡する（setState 無しなので再レンダー無し）
// Source of the grab-start position; grabs always begin with a mousedown, so track only mousedown (no setState, no re-render)
let lastPointerDown = { x: 0, y: 0 };
if (typeof window !== "undefined") {
  window.addEventListener(
    "mousedown",
    (e) => {
      lastPointerDown = { x: e.clientX, y: e.clientY };
    },
    { capture: true },
  );
}

// マウス追従の grab オーバーレイ。mousemove の再レンダリングをこのコンポーネント内に閉じ込める
// Cursor-following grab overlay; keeps mousemove re-renders contained to this component
export default function GrabOverlay({ grab }: { grab: SlotData }) {
  const [mousePos, setMousePos] = useState(lastPointerDown);

  // 掴んでいる間だけ mousemove 追従。掴んだ瞬間は描画前に mousedown 座標へ同期する（stale座標の一瞬表示を防ぐ）
  // Follow mousemove only while held; sync to the mousedown position before paint when a grab starts
  useLayoutEffect(() => {
    if (grab.count === 0) return;
    setMousePos(lastPointerDown);
    const onMove = (e: globalThis.MouseEvent) => setMousePos({ x: e.clientX, y: e.clientY });
    window.addEventListener("mousemove", onMove);
    return () => window.removeEventListener("mousemove", onMove);
  }, [grab.count]);

  if (grab.count === 0) return null;

  // 追従位置はカーソル座標の動的値なので inline style（module 化対象外）
  // Follow position is a dynamic cursor value, so inline style (not module-ized)
  return (
    <div data-testid="grab-overlay" className={styles.overlay} style={{ left: mousePos.x - 24, top: mousePos.y - 24 }}>
      <ItemSlot itemId={grab.itemId} count={grab.count} />
    </div>
  );
}
