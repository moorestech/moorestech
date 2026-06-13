import { useState } from "react";

type Props = {
  itemId: number;
  alt?: string;
  className?: string;
};

// アイコン画像と読み込み失敗時の #id フォールバックを共通化する
// Shared item icon with the #id fallback for load failures
export default function ItemIcon({ itemId, alt, className }: Props) {
  // アイコン読み込み失敗を itemId 単位で記録し、ID ラベル表示に切り替える
  // Track icon load failures per itemId and fall back to the id label
  const [erroredItemId, setErroredItemId] = useState<number | null>(null);

  if (erroredItemId === itemId) {
    return (
      <span className={`flex items-center justify-center text-[10px] text-gray-400 ${className ?? ""}`}>
        #{itemId}
      </span>
    );
  }

  return (
    <img
      src={`/api/icons/${itemId}.png`}
      alt={alt ?? `item ${itemId}`}
      className={className}
      draggable={false}
      onError={() => setErroredItemId(itemId)}
    />
  );
}
