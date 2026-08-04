import type { ViewportTransform } from "./viewport";

// パン・ズーム位置のセッション内ストア（画面を閉じても保持、リロードで消える）
// In-session store for pan/zoom transforms (survives screen close, cleared on reload)
const storedViewports = new Map<string, ViewportTransform>();

export function loadStoredViewport(key: string): ViewportTransform | null {
  return storedViewports.get(key) ?? null;
}

export function saveStoredViewport(key: string, viewport: ViewportTransform): void {
  storedViewports.set(key, viewport);
}
