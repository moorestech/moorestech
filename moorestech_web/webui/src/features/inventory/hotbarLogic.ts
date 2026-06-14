// ホットバー選択の純粋ロジック（入力キー変換と循環移動）
// Pure hotbar-selection logic: key mapping and circular cycling

// "1".."9" を 0..8 に変換し、それ以外は null（uGUI の HotBar 入力は 1-9 を返すため -1）
// Map "1".."9" to 0..8, else null (uGUI HotBar input returns 1-9, so subtract 1)
export function keyToHotbarIndex(key: string): number | null {
  if (key.length !== 1 || key < "1" || key > "9") return null;
  return key.charCodeAt(0) - "1".charCodeAt(0);
}

// 選択を循環移動する（負の delta でも常に 0..count-1 に収める）
// Cycle the selection circularly, always landing in 0..count-1 even for negative delta
export function cycleHotbar(current: number, delta: number, count: number): number {
  return (((current + delta) % count) + count) % count;
}
