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
