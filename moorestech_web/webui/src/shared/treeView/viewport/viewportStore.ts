import type { ViewportTransform } from "./viewport";

// セッション内ストア(リロードで消える)
// In-session store (cleared on reload)
const storedViewports = new Map<string, ViewportTransform>();

export function loadStoredViewport(key: string): ViewportTransform | null {
  return storedViewports.get(key) ?? null;
}

export function saveStoredViewport(key: string, viewport: ViewportTransform): void {
  storedViewports.set(key, viewport);
}
