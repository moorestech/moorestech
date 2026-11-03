import type { HTMLAttributes } from "react";
import type { TooltipProps } from "@mantine/core";
import HoverTooltip from "../HoverTooltip";
import ItemIcon from "../ItemIcon";
import SlotFrame from "../SlotFrame";
import { formatSlotAmount } from "../slotAmountFormat";
import styles from "./style.module.css";
import { L, useI18n, useItemNameResolver } from "@/shared/i18n";
import { type MaterialTooltipKey, useMaterialTooltipText } from "@/shared/materialTooltipText";

// 所持/必要を1枠に載せる素材表示。ツールチップ本文もキーから共有側で組み立てる
// The owned/required material presentation for one slot; the tooltip body is composed here from the key
// 赤字は lacking、枠の減光は insufficient と分ける。減光させず数字だけ赤くする用途（機械の素材スロット）があるため
// tooltipKey が null のときは通常のアイテム名ツールチップのまま（素材の語彙で語らない用途）
// Red text follows lacking while frame dimming follows insufficient, since some usages (machine material slots) redden only the numbers
// A null tooltipKey keeps the ordinary item-name tooltip (usages that should not speak in material vocabulary)
type Shortage = {
  ownedCount: number;
  requiredCount: number;
  lacking: boolean;
  tooltipKey: MaterialTooltipKey | null;
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
  // 所持/必要の数値。あれば個数バッジは出さない
  // Held/required numbers; hides the ordinary count badge
  shortage?: Shortage;
  // 空スロットに置く「入るべきアイテム」の半透明プレビュー。実物（itemId>0）があれば無視する
  // Translucent preview of the item that belongs in an empty slot; ignored when a real item (itemId>0) is present
  ghost?: { itemId: number; count: number };
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
export default function ItemSlot({ itemId, count, tooltip, selected, catalog, insufficient, shortage, ghost, onLeftDown, onRightDown, onRightEnter, onLeftEnter, onDoubleClick, onHoverChange, testId, ...divProps }: Props) {
  const { t } = useI18n();
  const resolveItemName = useItemNameResolver();
  const materialTooltipText = useMaterialTooltipText();

  // カタログは常にアイコンを出し、白面（filled）は所持数がある時だけ
  // Catalog always shows the icon; the white (filled) face applies only when an owned count exists
  const owned = count !== undefined && count > 0;
  const hasItem = itemId > 0 && (catalog || count === undefined || count > 0);
  const filled = catalog ? owned : hasItem;
  // 実物が無い空スロットに限りゴーストを描く。filled は実物のみで判定し白面化しない
  // The ghost draws only on a genuinely empty slot; filled stays real-item-only so a ghost never gets the white face
  const ghostShown = ghost !== undefined && !hasItem && ghost.itemId > 0;
  const shownItemId = ghostShown ? ghost.itemId : itemId;
  const shownCount = ghostShown ? ghost.count : count;
  const resolvedName = resolveItemName(shownItemId);

  const shortageTooltip = shortage === undefined || shortage.tooltipKey === null ? undefined : (
    <span style={{ whiteSpace: "pre-line" }}>
      {materialTooltipText(shortage.tooltipKey, itemId, shortage.requiredCount, shortage.ownedCount)}
    </span>
  );
  const label = shortageTooltip ?? tooltip;

  const slot = (
    <HoverTooltip label={label ?? resolvedName} disabled={!(hasItem || ghostShown) || (!label && !resolvedName)}>
      <SlotFrame
        {...divProps}
        testId={testId}
        selected={selected}
        filled={filled}
        catalog={catalog}
        insufficient={insufficient}
        ghost={ghostShown}
        onLeftDown={onLeftDown}
        onRightDown={onRightDown}
        onRightEnter={onRightEnter}
        onLeftEnter={onLeftEnter}
        onDoubleClick={onDoubleClick}
        onHoverChange={onHoverChange}
      >
        {hasItem || ghostShown ? (
          <>
            <ItemIcon itemId={shownItemId} fallback={{ kind: "idText" }} className={styles.icon} />
            {shortage === undefined && (owned || ghostShown) && shownCount !== undefined && shownCount > 0 ? <span className={`iconTextOutlineLight ${styles.count}`}>{formatSlotAmount(shownCount)}</span> : null}
          </>
        ) : null}
      </SlotFrame>
    </HoverTooltip>
  );

  // 要素型を固定し、実物の出し入れで shortage の有無が変わってもツールチップ等を再マウントさせない
  // The wrapper is always present so toggling shortage never remounts the tooltip or frame
  return (
    <div className={styles.shortageSlot}>
      {slot}
      {shortage === undefined ? null : (
        <span className={`iconTextOutlineLight ${styles.shortageCount}`} data-lack={shortage.lacking || undefined}>
          {t(L.ui.recipe.itemCountSummary, { ownedCount: formatSlotAmount(shortage.ownedCount), requiredCount: formatSlotAmount(shortage.requiredCount) })}
        </span>
      )}
    </div>
  );
}
