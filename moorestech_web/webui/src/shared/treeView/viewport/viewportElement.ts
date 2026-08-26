import { readTutorialAnchorClipInsetPx } from "@/shared/tutorialAnchor";

// stageの拡縮ぶんを打ち消し、実画面の移動量をキャンバス座標の移動量へ直す
// Cancels the stage's scaling so a real-screen movement becomes a canvas-space movement
export const toCssScale = (element: HTMLDivElement) => element.offsetWidth / element.getBoundingClientRect().width;

// キャンバス原点はviewportの内容box。クリップ逃げのpaddingぶん枠線boxからずれるため座標計算はここを基準にする
// The canvas sits at the viewport's content box, offset from the border box by the clip clearance, so all coordinate math uses it
export const toContentBox = (element: HTMLDivElement) => {
  const insetPx = readTutorialAnchorClipInsetPx();
  return {
    left: insetPx, top: insetPx,
    width: element.offsetWidth - insetPx * 2,
    height: element.offsetHeight - insetPx * 2,
  };
};
