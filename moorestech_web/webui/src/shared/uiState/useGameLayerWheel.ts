import { useEffect, useRef } from "react";
import { readActiveLayer } from "./activeLayer";

// game レイヤー時のみ発火するグローバル wheel。カーソルロック中はHUD上をホバーできないため window で受ける
// Global wheel firing only at the game layer; the cursor is locked during play, so the window receives it instead of any HUD element
export function useGameLayerWheel(handler: (e: WheelEvent) => void): void {
  // リスナーは1回だけ張り、最新の handler は ref 経由で呼ぶ
  // Attach the listener once and call the latest handler through a ref
  const handlerRef = useRef(handler);
  handlerRef.current = handler;

  useEffect(() => {
    const onWheel = (e: WheelEvent) => {
      if (readActiveLayer() !== "game") return;
      handlerRef.current(e);
    };
    window.addEventListener("wheel", onWheel);
    return () => window.removeEventListener("wheel", onWheel);
  }, []);
}
