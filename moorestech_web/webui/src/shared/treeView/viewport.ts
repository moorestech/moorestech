import type { TreePoint } from "./treeGeometry";

export type ViewportTransform = { x: number; y: number; scale: number };

const MIN_VIEW_SCALE = 0.4;
const MAX_VIEW_SCALE = 2.5;
const WHEEL_ZOOM_SENSITIVITY = 0.0015;

export function zoomViewportAt(
  viewport: ViewportTransform,
  cursor: TreePoint,
  deltaY: number,
): ViewportTransform {
  const scale = Math.min(
    MAX_VIEW_SCALE,
    Math.max(MIN_VIEW_SCALE, viewport.scale * Math.exp(-deltaY * WHEEL_ZOOM_SENSITIVITY)),
  );
  const worldX = (cursor.x - viewport.x) / viewport.scale;
  const worldY = (cursor.y - viewport.y) / viewport.scale;
  return { x: cursor.x - worldX * scale, y: cursor.y - worldY * scale, scale };
}

export type ViewportSize = { width: number; height: number };

// キャンバス上の1点をビューポート中央に据える変換を返す
// Returns the transform that places one canvas point at the viewport center
export function centerViewportOn(
  canvasPoint: TreePoint,
  viewSize: ViewportSize,
  scale: number,
): ViewportTransform {
  return {
    x: viewSize.width / 2 - canvasPoint.x * scale,
    y: viewSize.height / 2 - canvasPoint.y * scale,
    scale,
  };
}

// 慣性パンの物理定数（速度はキャンバスpx/ms）
// Inertial-pan physics constants (velocity in canvas px per ms)
export const PAN_FRICTION_TAU_MS = 325;
export const PAN_MIN_FLING_SPEED = 0.15;
export const PAN_STOP_SPEED = 0.01;
export const PAN_MAX_FLING_SPEED = 3;
export const PAN_RELEASE_STALL_MS = 80;
export const PAN_VELOCITY_SMOOTHING_TAU_MS = 50;

export type PanVelocity = { x: number; y: number };

// 経過時間ぶん速度を指数減衰させる
// Exponentially decays the velocity by the elapsed time
export function decayPanVelocity(velocity: PanVelocity, dtMs: number): PanVelocity {
  const keep = Math.exp(-dtMs / PAN_FRICTION_TAU_MS);
  return { x: velocity.x * keep, y: velocity.y * keep };
}

// 直近の移動サンプルで速度を平滑更新する（急停止直後の外れ値を均す）
// Smoothly updates velocity with the latest move sample (evens out outliers)
export function blendPanVelocity(velocity: PanVelocity, dx: number, dy: number, dtMs: number): PanVelocity {
  const alpha = 1 - Math.exp(-dtMs / PAN_VELOCITY_SMOOTHING_TAU_MS);
  return {
    x: velocity.x + (dx / dtMs - velocity.x) * alpha,
    y: velocity.y + (dy / dtMs - velocity.y) * alpha,
  };
}

// 発動上限を超える速度を向きを保ったまま丸める
// Clamps speed above the fling cap while preserving direction
export function clampPanVelocity(velocity: PanVelocity): PanVelocity {
  const speed = Math.hypot(velocity.x, velocity.y);
  if (speed <= PAN_MAX_FLING_SPEED) return velocity;
  const ratio = PAN_MAX_FLING_SPEED / speed;
  return { x: velocity.x * ratio, y: velocity.y * ratio };
}
