export type TreePoint = { x: number; y: number };
export type TreeNodePosition = TreePoint & { id: string };
export type TreeCanvasBounds = { width: number; height: number; offsetX: number; offsetY: number };
export type TreeLine = TreePoint & { length: number; angleDeg: number };

export function computeTreeCanvasBounds(nodes: TreeNodePosition[], padding: number): TreeCanvasBounds {
  if (nodes.length === 0) {
    return { width: padding * 2, height: padding * 2, offsetX: padding, offsetY: padding };
  }
  const xs = nodes.map((node) => node.x);
  const ys = nodes.map((node) => node.y);
  const minX = Math.min(...xs);
  const maxX = Math.max(...xs);
  const minY = Math.min(...ys);
  const maxY = Math.max(...ys);
  return {
    width: maxX - minX + padding * 2,
    height: maxY - minY + padding * 2,
    offsetX: padding - minX,
    offsetY: maxY + padding,
  };
}

export function toTreeCanvasPoint(point: TreePoint, bounds: TreeCanvasBounds): TreePoint {
  return { x: point.x + bounds.offsetX, y: bounds.offsetY - point.y };
}

export function lineBetween(from: TreePoint, to: TreePoint): TreeLine {
  const dx = to.x - from.x;
  const dy = to.y - from.y;
  return { x: from.x, y: from.y, length: Math.hypot(dx, dy), angleDeg: (Math.atan2(dy, dx) * 180) / Math.PI };
}
