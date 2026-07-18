import type { MouseEvent } from "react";

type LeftDownHandler = (shiftKey: boolean) => void;
type RightDownHandler = () => void;
type RightEnterHandler = () => void;

// スロットの左右押下とメニュー抑止を共通化する
// Shares slot left/right presses and context-menu suppression
export function useSlotMouse(onLeftDown: LeftDownHandler | undefined, onRightDown: RightDownHandler | undefined, onRightEnter?: RightEnterHandler) {
  const onMouseDown = (event: MouseEvent<HTMLElement>) => {
    event.preventDefault();
    if (event.button === 0) onLeftDown?.(event.shiftKey);
    if (event.button === 2) onRightDown?.();
  };

  // 右保持中のスロット通過を通知する
  // Notify continuous placement when entering a slot while the right button is held
  const onMouseEnter = (event: MouseEvent<HTMLElement>) => {
    if ((event.buttons & 2) !== 0) onRightEnter?.();
  };

  // 右押下処理後にブラウザのコンテキストメニューが重なるのを防ぐ
  // Prevents the browser context menu from covering the slot after a right press
  const onContextMenu = (event: MouseEvent<HTMLElement>) => event.preventDefault();

  return { onMouseDown, onMouseEnter, onContextMenu };
}
