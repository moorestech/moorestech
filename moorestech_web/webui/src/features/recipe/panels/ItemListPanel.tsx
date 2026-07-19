import { useMemo } from "react";
import type { CSSProperties } from "react";
import { ScrollArea } from "@mantine/core";
import { useTopic, Topics } from "@/bridge";
import { ConnectingPlaceholder, ItemSlot, SlotGrid, GamePanel } from "@/shared/ui";
import { buildOwnedCounts } from "@/shared/ownedCounts";
import { useItemSelectionStore } from "../logic/selectionStore";
import { craftableResultCounts } from "../logic/craftLogic";
import { useI18n } from "@/shared/i18n";
import styles from "./ItemListPanel.module.css";
import { tutorialAnchor, type AnchorId } from "@/shared/tutorialAnchor";
import { useDragScroll } from "./useDragScroll";

// 固定pxで6列のピッチを均一化する
// Use fixed-pixel tracks and gaps to keep all six catalog columns on a uniform 140px screenshot pitch
// カタログは2.5/3/3.5/4/4.5pxでrec-whiteまたは列検出が崩れたため、両方を守る実測下限5pxを使う
// Use the measured 5px catalog floor because 2.5/3/3.5/4/4.5px broke rec-white or column detection
const GRID_STYLE = { "--slot-size": "46.144px", "--slot-grid-gap": "8.656px", "--slot-grid-row-gap": "8.896px", "--icon-pad": "5px", "--count-bottom": "-1px", "--count-font-size": "16px", "--count-letter-spacing": "0.12em" } as CSSProperties;

// 右カラム: 表示対象アイテムの一覧（uGUI の ItemListView 準拠）。クリックで中央にレシピ表示
// Right column: list of viewable items, like uGUI's ItemListView; click shows recipes in the center
export default function ItemListPanel() {
  const { t } = useI18n();
  const onSelect = useItemSelectionStore((s) => s.setSelectedItem);

  // 掴んでドラッグでもスクロールできるようにする。ドラッグ確定時は選択せず、タップ時のみ選択
  // Enable grab-drag scrolling; a committed drag does not select, only a tap does
  const { dragging, viewportHandlers } = useDragScroll({
    onTap: (target) => {
      const el = target.closest<HTMLElement>("[data-item-id]");
      if (el) onSelect(Number(el.dataset.itemId));
    },
  });
  const itemList = useTopic(Topics.itemList);
  const inventory = useTopic(Topics.inventory);
  const craftRecipes = useTopic(Topics.craftRecipes);

  // 素材所持数を制作可能数へ変換する
  // Aggregate materials across main+hotbar, then derive craftable counts for catalog badges
  const ownedCounts = useMemo(
    () => buildOwnedCounts(inventory ? [...inventory.mainSlots, ...inventory.hotbarSlots] : []),
    [inventory],
  );
  const craftableCounts = useMemo(
    () => craftableResultCounts(craftRecipes?.recipes ?? [], ownedCounts),
    [craftRecipes, ownedCounts],
  );

  return (
    <GamePanel
      gridArea="items"
      title={t("CRAFT RECIPE")}
      style={{ justifySelf: "end", alignSelf: "start", width: 378, minHeight: 452, "--panel-top": "-6.821px", "--panel-bottom": "-9.17px", "--panel-left": "-1.04px", "--title-shift-x": "1.57px", "--title-scale-x": 0.963, "--title-scale-y": 0.861 } as CSSProperties}
    >
      {itemList ? (
        // mahは7段が丸ごと収まりつつDEMO60件(10段)でノブ比が正本≈70%になる高さ。marginLeftはグリッド内側
        // インデント補正、marginTopはノブの縦位置合わせ。align-self:stretchだとmarginLeftだけでは右端(ノブ位置)が
        // 動かないためmarginRightで右端を別途詰める
        // mah fits 7 full rows while making the DEMO 60-item (10-row) thumb ratio match the reference ~70%. marginLeft
        // corrects the grid inset; marginTop aligns the knob vertically. Under align-self:stretch, marginLeft alone
        // doesn't move the right edge (knob position), so marginRight tucks the right edge in separately
        <ScrollArea.Autosize
          mah={381.2}
          type="always"
          scrollbarSize={4}
          className={styles.scrollArea}
          style={{ marginLeft: -3.561498, marginRight: 4.435, marginTop: 12 }}
          // ドラッグ中だけカーソルを掴み表示にしテキスト選択を抑止する（touch-actionはCSS側）
          // Only while dragging, show a grabbing cursor and suppress text selection (touch-action lives in CSS)
          viewportProps={{ ...viewportHandlers, style: { cursor: dragging ? "grabbing" : undefined, userSelect: dragging ? "none" : undefined } }}
        >
          <SlotGrid cols={6} testId="item-list-grid" style={GRID_STYLE}>
            {itemList.itemIds.map((id) => (
              <div key={id} data-item-id={id} {...tutorialAnchor(`recipe.item-${id}` as AnchorId)}>
                <ItemSlot
                  itemId={id}
                  count={craftableCounts.get(id) ?? 0}
                  catalog
                />
              </div>
            ))}
          </SlotGrid>
        </ScrollArea.Autosize>
      ) : (
        <ConnectingPlaceholder />
      )}
    </GamePanel>
  );
}
