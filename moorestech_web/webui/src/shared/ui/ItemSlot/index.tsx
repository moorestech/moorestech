import { Tooltip } from "@mantine/core";
import ItemIcon from "../ItemIcon";
import SlotFrame from "../SlotFrame";
import styles from "./style.module.css";

type Props = {
  itemId: number;
  // count 省略時は個数バッジを表示せず、itemId>0 ならアイコンのみ表示する
  // When count is omitted, the count badge is hidden and the icon shows for itemId>0
  count?: number;
  name?: string;
  selected?: boolean;
  // catalog はレシピ一覧用。未所持は灰面＋アイコン、所持(count>0)のみ白面＋個数
  // "catalog" is for the recipe list: unowned shows a gray face + icon, only owned (count>0) shows a white face + count
  catalog?: boolean;
  onLeftDown?: (shiftKey: boolean) => void;
  onRightDown?: () => void;
  onDoubleClick?: () => void;
  testId?: string;
};

// アイコン・個数・ホバーツールチップ付きの汎用アイテムスロット
// Generic item slot with icon, count, and a hover tooltip
export default function ItemSlot({ itemId, count, name, selected, catalog, onLeftDown, onRightDown, onDoubleClick, testId }: Props) {
  // カタログは常にアイコンを出し、白面（filled）は所持数がある時だけ
  // Catalog always shows the icon; the white (filled) face applies only when an owned count exists
  const owned = count !== undefined && count > 0;
  const hasItem = itemId > 0 && (catalog || count === undefined || count > 0);
  const filled = catalog ? owned : hasItem;

  return (
    // Tooltip は子要素をラップせず cloneElement するため DOM 構造（grid > div）は不変
    // Tooltip clones the child without a wrapper, keeping the grid > div DOM shape intact
    <Tooltip label={name} disabled={!hasItem || !name}>
      <SlotFrame
        testId={testId}
        selected={selected}
        filled={filled}
        catalog={catalog}
        onLeftDown={onLeftDown}
        onRightDown={onRightDown}
        onDoubleClick={onDoubleClick}
      >
        {hasItem ? (
          <>
            <ItemIcon itemId={itemId} alt={name ?? `item ${itemId}`} className={styles.icon} />
            {count !== undefined ? <span className={styles.count}>{count}</span> : null}
          </>
        ) : null}
      </SlotFrame>
    </Tooltip>
  );
}
