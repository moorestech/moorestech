import { useState } from "react";
import styles from "./ItemIcon.module.css";

type Props = {
  blockId: number;
  alt?: string;
  className?: string;
};

// ブロックアイコン画像と読み込み失敗時の #id フォールバックを共通化する
// Shared block icon with the #id fallback for load failures
export default function BlockIcon({ blockId, alt, className }: Props) {
  // アイコン読み込み失敗を blockId 単位で記録し、ID ラベル表示に切り替える
  // Track icon load failures per blockId and fall back to the id label
  const [erroredBlockId, setErroredBlockId] = useState<number | null>(null);

  if (erroredBlockId === blockId) {
    return <span className={`${styles.fallback} ${className ?? ""}`}>#{blockId}</span>;
  }

  return (
    <img
      src={`/api/block-icons/${blockId}.png`}
      alt={alt ?? `block ${blockId}`}
      className={className}
      draggable={false}
      onError={() => setErroredBlockId(blockId)}
    />
  );
}
