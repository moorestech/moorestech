import { useEffect, useState } from "react";
import { ItemSlot } from "@/shared/ui";
import type { SlotData } from "@/bridge/payloadTypes";
import styles from "./GrabOverlay.module.css";

// マウス追従の grab オーバーレイ。mousemove の再レンダリングをこのコンポーネント内に閉じ込める
// Cursor-following grab overlay; keeps mousemove re-renders contained to this component
export default function GrabOverlay({ grab }: { grab: SlotData }) {
  const [mousePos, setMousePos] = useState({ x: 0, y: 0 });

  // grab を保持している間だけ mousemove を購読する（未保持時の毎回再レンダーを避ける）
  // Subscribe to mousemove only while a grab is held (avoids re-renders when nothing is grabbed)
  useEffect(() => {
    if (grab.count === 0) return;
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
