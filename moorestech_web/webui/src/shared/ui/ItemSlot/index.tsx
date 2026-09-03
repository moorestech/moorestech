import type { HTMLAttributes } from "react";
import type { TooltipProps } from "@mantine/core";
import HoverTooltip from "../HoverTooltip";
import ItemIcon from "../ItemIcon";
import SlotFrame from "../SlotFrame";
import styles from "./style.module.css";
import { L, useI18n, useItemNameResolver } from "@/shared/i18n";
import { type MaterialTooltipKey, useMaterialTooltipText } from "@/shared/materialTooltipText";

// 所持/必要を1枠に載せる素材表示。ツールチップ本文もキーから共有側で組み立てる
// The owned/required material presentation for one slot; the tooltip body is composed here from the key
type Shortage = {
  ownedCount: number;
  requiredCount: number;
  tooltipKey: MaterialTooltipKey;
};

// 素のdiv属性はSlotFrameへ素通しする。呼び出し側の関心事（アンカー等）に共有側が名前を与えないため
// Bare div attributes pass straight through to SlotFrame so the shared part never names the caller's concerns (anchors etc.)
type Props = Omit<HTMLAttributes<HTMLDivElement>, "children" | "onMouseDown" | "onDoubleClick" | "onContextMenu" | "onMouseEnter" | "onMouseLeave"> & {
  itemId: number;
  // count が未指定か0なら個数バッジを出さず、itemId>0 ならアイコンのみ表示する
  // With count undefined or 0 the badge is hidden, and the icon shows for itemId>0
  count?: number;
  tooltip?: TooltipProps["label"];
  selected?: boolean;
  // catalog はレシピ一覧用。未所持は灰面＋アイコン、所持(count>0)のみ白面＋個数
  // "catalog" is for the recipe list: unowned shows a gray face + icon, only owned (count>0) shows a white face + count
  catalog?: boolean;
  insufficient?: boolean;
  // 赤字にするかは insufficient が決める。数値と文言だけをここが持つ
  // insufficient decides the red text; this only carries the numbers and the wording
  shortage?: Shortage;
  onLeftDown?: (shiftKey: boolean) => void;
  onRightDown?: () => void;
  onRightEnter?: () => void;
  onLeftEnter?: () => void;
  onDoubleClick?: () => void;
  onHoverChange?: (hovering: boolean) => void;
  testId?: string;
};

// アイコン・個数・ホバーツールチップ付きの汎用アイテムスロット
// Generic item slot with icon, count, and a hover tooltip
export default function ItemSlot({ itemId, count, tooltip, selected, catalog, insufficient, shortage, onLeftDown, onRightDown, onRightEnter, onLeftEnter, onDoubleClick, onHoverChange, testId, ...divProps }: Props) {
  const { t } = useI18n();
  const resolveItemName = useItemNameResolver();
  const resolvedName = resolveItemName(itemId);
  const materialTooltipText = useMaterialTooltipText();

  // カタログは常にアイコンを出し、白面（filled）は所持数がある時だけ
  // Catalog always shows the icon; the white (filled) face applies only when an owned count exists
  const owned = count !== undefined && count > 0;
  const hasItem = itemId > 0 && (catalog || count === undefined || count > 0);
  const filled = catalog ? owned : hasItem;

  const shortageTooltip = shortage === undefined ? undefined : (
    <span style={{ whiteSpace: "pre-line" }}>
      {materialTooltipText(shortage.tooltipKey, itemId, shortage.requiredCount, shortage.ownedCount)}
    </span>
  );
  const label = shortageTooltip ?? tooltip;

  const slot = (
    // Tooltip は子要素をラップせず cloneElement するため DOM 構造（grid > div）は不変
    // The tooltip clones the child without a wrapper, keeping the grid > div DOM shape intact
    <HoverTooltip label={label ?? resolvedName} disabled={!hasItem || (!label && !resolvedName)}>
      <SlotFrame
        {...divProps}
        testId={testId}
        selected={selected}
        filled={filled}
        catalog={catalog}
        insufficient={insufficient}
        onLeftDown={onLeftDown}
        onRightDown={onRightDown}
        onRightEnter={onRightEnter}
        onLeftEnter={onLeftEnter}
        onDoubleClick={onDoubleClick}
        onHoverChange={onHoverChange}
      >
        {hasItem ? (
          <>
            <ItemIcon itemId={itemId} alt={resolvedName ?? t(L.ui.common.itemFallback, { itemId })} className={styles.icon} />
            {owned ? <span className={`iconTextOutlineLight ${styles.count}`}>{count}</span> : null}
          </>
        ) : null}
      </SlotFrame>
    </HoverTooltip>
  );

  if (shortage === undefined) return slot;

  // 所持/必要の数値は枠外へはみ出し、不足減光(opacity)も受けないため枠の外側に置く
  // The owned/required numbers overflow the frame and must escape its shortage dimming, so they sit outside it
  return (
    <div className={styles.shortageSlot}>
      {slot}
      <span className={`iconTextOutlineLight ${styles.shortageCount}`} data-lack={insufficient || undefined}>
        {t(L.ui.recipe.itemCountSummary, { ownedCount: shortage.ownedCount, requiredCount: shortage.requiredCount })}
      </span>
    </div>
  );
}
