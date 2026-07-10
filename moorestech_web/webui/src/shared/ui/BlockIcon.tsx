import { useState } from "react";
import styles from "./ItemIcon.module.css";

type Props = {
  blockId: number;
  alt?: string;
  className?: string;
};

// 失敗時は #id 表示
// Falls back to #id on failure
export default function BlockIcon({ blockId, alt, className }: Props) {
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
