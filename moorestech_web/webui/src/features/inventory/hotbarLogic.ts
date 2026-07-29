// ホットバー選択の純粋ロジック（入力キー変換）
// Pure hotbar-selection logic: key mapping

// "1".."9" を 0..8 に変換し、それ以外は null（uGUI の HotBar 入力は 1-9 を返すため -1）
// Map "1".."9" to 0..8, else null (uGUI HotBar input returns 1-9, so subtract 1)
export function keyToHotbarIndex(key: string): number | null {
  if (key.length !== 1 || key < "1" || key > "9") return null;
  return key.charCodeAt(0) - "1".charCodeAt(0);
}
