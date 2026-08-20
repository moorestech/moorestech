// アイテムごとに先頭スロットだけをアンカー担当にする。同名アンカーの重複はresolverが不一致扱いにするため
// Only the first slot per item carries the anchor; duplicate anchor names would be rejected by the resolver
export function firstSlotIndexByItemId(slots: ReadonlyArray<{ itemId: number }>): Map<number, number> {
  const result = new Map<number, number>();
  slots.forEach((slot, index) => {
    if (slot.itemId <= 0 || result.has(slot.itemId)) return;
    result.set(slot.itemId, index);
  });
  return result;
}
