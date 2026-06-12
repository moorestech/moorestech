import type { MouseEvent } from "react";

type Props = {
  itemId: number;
  count: number;
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

  const hasItem = itemId > 0 && count > 0;

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
          <img
            src={`/api/icons/${itemId}.png`}
            alt={name ?? `item ${itemId}`}
            className="w-full h-full object-contain p-0.5"
            draggable={false}
            onError={(e) => {
              // アイコン未配信（503等）は壊れ画像を隠してID表示に切り替える
              // Hide broken icons (e.g. 503 before ready) and fall back to the id label
              e.currentTarget.style.display = "none";
              e.currentTarget.nextElementSibling?.classList.remove("hidden");
            }}
          />
          <span className="hidden absolute inset-0 flex items-center justify-center text-[10px] text-gray-400">
            #{itemId}
          </span>
          <span className="absolute bottom-0 right-0.5 text-xs text-green-300 font-bold drop-shadow">{count}</span>
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
