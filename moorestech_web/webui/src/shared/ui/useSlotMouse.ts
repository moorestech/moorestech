import type { MouseEvent } from "react";

type LeftDownHandler = (shiftKey: boolean) => void;
type RightDownHandler = () => void;

// スロットの左右押下とメニュー抑止を共通化する
// Shares slot left/right presses and context-menu suppression
export function useSlotMouse(onLeftDown: LeftDownHandler | undefined, onRightDown: RightDownHandler | undefined) {
  const onMouseDown = (event: MouseEvent<HTMLElement>) => {
    event.preventDefault();
    if (event.button === 0) onLeftDown?.(event.shiftKey);
    if (event.button === 2) onRightDown?.();
  };

  // 右押下処理後にブラウザのコンテキストメニューが重なるのを防ぐ
  // Prevents the browser context menu from covering the slot after a right press
  const onContextMenu = (event: MouseEvent<HTMLElement>) => event.preventDefault();

  return { onMouseDown, onContextMenu };
}
