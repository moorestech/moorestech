import type { MouseEvent } from "react";
import ItemIcon from "./ItemIcon";

type Props = {
  itemId: number;
  // count 省略時は個数バッジを表示せず、itemId>0 ならアイコンのみ表示する
  // When count is omitted, the count badge is hidden and the icon shows for itemId>0
  count?: number;
  name?: string;
  selected?: boolean;
  onLeftDown?: (shiftKey: boolean) => void;
  onRightDown?: () => void;
  onDoubleClick?: () => void;
};

// アイコン・個数・ホバーツールチップ付きの汎用アイテムスロット
// Generic item slot with icon, count, and a hover tooltip
export default function ItemSlot({ itemId, count, name, selected, onLeftDown, onRightDown, onDoubleClick }: Props) {
  const onMouseDown = (e: MouseEvent) => {
    e.preventDefault();
    if (e.button === 0) onLeftDown?.(e.shiftKey);
    if (e.button === 2) onRightDown?.();
  };

  const hasItem = itemId > 0 && (count === undefined || count > 0);

  return (
    <div
      className={`group relative w-12 h-12 border rounded bg-gray-900 select-none ${
        selected ? "border-yellow-400" : "border-gray-700"
      }`}
      onMouseDown={onMouseDown}
      onDoubleClick={onDoubleClick}
      onContextMenu={(e) => e.preventDefault()}
    >
      {hasItem ? (
        <>
          <ItemIcon itemId={itemId} alt={name ?? `item ${itemId}`} className="w-full h-full object-contain p-0.5" />
          {count !== undefined ? (
            <span className="absolute bottom-0 right-0.5 text-xs text-green-300 font-bold drop-shadow">{count}</span>
          ) : null}
          {name ? (
            <span className="pointer-events-none absolute bottom-full left-1/2 -translate-x-1/2 mb-1 hidden group-hover:block whitespace-nowrap bg-black/90 text-white text-xs rounded px-2 py-1 z-20">
              {name}
            </span>
          ) : null}
        </>
      ) : null}
    </div>
  );
}
