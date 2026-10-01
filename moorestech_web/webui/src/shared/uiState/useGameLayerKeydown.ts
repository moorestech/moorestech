import { useEffect, useRef } from "react";
import { isTextInputElement, readActiveLayer } from "./activeLayer";

// game レイヤー時のみ発火するグローバル keydown。入力欄フォーカス中はゲーム操作を奪わない
// Global keydown firing only at the game layer; never hijacks typing while an input is focused
export function useGameLayerKeydown(handler: (e: KeyboardEvent) => void): void {
  // リスナーは1回だけ張り、最新の handler は ref 経由で呼ぶ
  // Attach the listener once and call the latest handler through a ref
  const handlerRef = useRef(handler);
  handlerRef.current = handler;

  useEffect(() => {
    const onKeyDown = (e: KeyboardEvent) => {
      if (isTextInputElement(document.activeElement)) return;
      if (readActiveLayer() !== "game") return;
      handlerRef.current(e);
    };
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, []);
}
