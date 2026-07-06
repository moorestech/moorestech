import type { MouseEvent } from "react";
import { Tooltip } from "@mantine/core";
import ItemIcon from "../ItemIcon";
import styles from "./style.module.css";

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
  testId?: string;
};

// アイコン・個数・ホバーツールチップ付きの汎用アイテムスロット
// Generic item slot with icon, count, and a hover tooltip
export default function ItemSlot({ itemId, count, name, selected, onLeftDown, onRightDown, onDoubleClick, testId }: Props) {
  const onMouseDown = (e: MouseEvent) => {
    e.preventDefault();
    if (e.button === 0) onLeftDown?.(e.shiftKey);
    if (e.button === 2) onRightDown?.();
  };

  const hasItem = itemId > 0 && (count === undefined || count > 0);

  return (
    // Tooltip は子要素をラップせず cloneElement するため DOM 構造（grid > div）は不変
    // Tooltip clones the child without a wrapper, keeping the grid > div DOM shape intact
    <Tooltip label={name} disabled={!hasItem || !name}>
      <div
        className={styles.slot}
        data-testid={testId}
        data-selected={selected ? "true" : undefined}
        onMouseDown={onMouseDown}
        onDoubleClick={onDoubleClick}
        onContextMenu={(e) => e.preventDefault()}
      >
        {hasItem ? (
          <>
            <ItemIcon itemId={itemId} alt={name ?? `item ${itemId}`} className={styles.icon} />
            {count !== undefined ? <span className={styles.count}>{count}</span> : null}
          </>
        ) : null}
      </div>
    </Tooltip>
  );
}
