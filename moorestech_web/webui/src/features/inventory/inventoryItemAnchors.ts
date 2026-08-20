// 重複アンカーは解決不能なので先頭スロットだけ担当する
// Duplicate anchors never resolve, so only the first slot carries it
export function firstSlotIndexByItemId(slots: ReadonlyArray<{ itemId: number }>): Map<number, number> {
  const result = new Map<number, number>();
  slots.forEach((slot, index) => {
    if (slot.itemId <= 0 || result.has(slot.itemId)) return;
    result.set(slot.itemId, index);
  });
  return result;
}
