import { useEffect, useState } from "react";
import { ItemSlot } from "@/shared/ui";
import type { SlotData } from "@/bridge/payloadTypes";

// マウス追従の grab オーバーレイ。mousemove の再レンダリングをこのコンポーネント内に閉じ込める
// Cursor-following grab overlay; keeps mousemove re-renders contained to this component
export default function GrabOverlay({ grab }: { grab: SlotData }) {
  const [mousePos, setMousePos] = useState({ x: 0, y: 0 });

  useEffect(() => {
    const onMove = (e: globalThis.MouseEvent) => setMousePos({ x: e.clientX, y: e.clientY });
    window.addEventListener("mousemove", onMove);
    return () => window.removeEventListener("mousemove", onMove);
  }, []);

  if (grab.count === 0) return null;

  // 追従位置はカーソル座標の動的値なので inline style（module 化対象外）
  // Follow position is a dynamic cursor value, so inline style (not module-ized)
  return (
    <div className="pointer-events-none fixed z-40 w-12 h-12" style={{ left: mousePos.x - 24, top: mousePos.y - 24 }}>
      <ItemSlot itemId={grab.itemId} count={grab.count} />
    </div>
  );
}
