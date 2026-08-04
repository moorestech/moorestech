import type { TreePoint } from "../treeGeometry";

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

// 慣性パン定数(速度=px/ms)
// Inertial-pan constants (velocity in px/ms)
// τ=325msはiOS系スクロール減衰の標準値。上限3px/msで最大滑走距離は3×325≒975pxに収まる
// τ=325ms is the standard iOS-style scroll decay; the 3px/ms cap bounds the glide at 3×325≒975px
export const PAN_FRICTION_TAU_MS = 325;
export const PAN_MIN_FLING_SPEED = 0.15;
export const PAN_STOP_SPEED = 0.01;
export const PAN_MAX_FLING_SPEED = 3;
// move間隔上限(超えたら再計算)
// Max move-sample gap (reset beyond this)
export const PAN_VELOCITY_SAMPLE_MAX_GAP_MS = 80;
// 離す直前の静止判定しきい値
// Release stall threshold (no fling beyond this)
export const PAN_RELEASE_STALL_MS = 80;
export const PAN_VELOCITY_SMOOTHING_TAU_MS = 50;
// 滑走1フレームの積分時間上限（タブ非アクティブ等の巨大間隔で吹き飛ばない）
// Per-frame integration cap for the glide (avoids teleporting after a background-tab gap)
export const PAN_MAX_FRAME_DELTA_MS = 64;

export type PanVelocity = { x: number; y: number };

// 経過時間ぶん速度を指数減衰させる
// Exponentially decays the velocity by the elapsed time
export function decayPanVelocity(velocity: PanVelocity, dtMs: number): PanVelocity {
  const keep = Math.exp(-dtMs / PAN_FRICTION_TAU_MS);
  return { x: velocity.x * keep, y: velocity.y * keep };
}

// 直近サンプルで速度を平滑更新(急停止の外れ値を均す)
// Smooths velocity with the latest sample (evens out outliers)
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
