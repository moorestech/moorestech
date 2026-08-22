import { useMemo } from "react";
import type { CSSProperties } from "react";
import { ScrollArea } from "@mantine/core";
import { useTopic, Topics } from "@/bridge";
import { ConnectingPlaceholder, ItemSlot, SlotGrid, GamePanel } from "@/shared/ui";
import { buildOwnedCounts } from "@/shared/ownedCounts";
import { useItemSelectionStore } from "../logic/selectionStore";
import { craftableResultCounts } from "../logic/craftLogic";
import { L, useI18n } from "@/shared/i18n";
import styles from "./ItemListPanel.module.css";
import { tutorialAnchor, recipeItemAnchorId } from "@/shared/tutorialAnchor";
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
  // Aggregate materials across the main inventory, then derive craftable counts for catalog badges
  const ownedCounts = useMemo(() => buildOwnedCounts(inventory?.mainSlots ?? []), [inventory]);
  // 3topic揃うまで描かない。0個バッジ非表示と読込中が見分けられなくなるため
  // Wait for all three topics: otherwise loading looks identical to a genuine zero-craftable badge
  const ready = itemList && craftRecipes && inventory;
  const craftableCounts = useMemo(
    () => craftableResultCounts(craftRecipes?.recipes ?? [], ownedCounts),
    [craftRecipes, ownedCounts],
  );

  return (
    <GamePanel
      gridArea="items"
      title={t(L.ui.recipe.catalogTitle)}
      // heightはminHeightではなく固定高。floorだけだと件数でパネルごと伸び、
      // 「パネルからはみ出した時に初めてスクロール」が成立しない（ユーザー裁定 2026-08-22）
      // A fixed height, not a floor: with only minHeight the panel grows with the item count and
      // "scroll only once it overflows the panel" never holds (user ruling 2026-08-22)
      style={{ justifySelf: "end", alignSelf: "start", width: 378, height: 452, "--panel-top": "-6.821px", "--panel-bottom": "-9.17px", "--panel-left": "-1.04px", "--title-shift-x": "1.57px", "--title-scale-x": 0.963, "--title-scale-y": 0.861 } as CSSProperties}
    >
      {ready ? (
        // marginLeftはグリッド内側インデント補正、marginTopはノブの縦位置合わせ。align-self:stretchだと
        // marginLeftだけでは右端(ノブ位置)が動かないためmarginRightで右端を別途詰める
        // marginLeft corrects the grid inset; marginTop aligns the knob vertically. Under align-self:stretch,
        // marginLeft alone doesn't move the right edge (knob position), so marginRight tucks the right edge in
        // typeはauto。alwaysは溢れが無くてもつまみ幅0の水平バーが黒帯として残る（ユーザー裁定 2026-08-17）
        // type stays auto: always leaves a zero-thumb horizontal bar as a black band even with no overflow (user ruling 2026-08-17)
        <ScrollArea
          type="auto"
          scrollbarSize={4}
          className={styles.scrollArea}
          style={{ marginLeft: -3.561498, marginRight: 4.435, marginTop: 12 }}
          // ドラッグ中のみ掴みカーソル表示
          // Grabbing cursor only while dragging
          viewportProps={{ ...viewportHandlers, style: { cursor: dragging ? "grabbing" : undefined } }}
        >
          <SlotGrid cols={6} testId="item-list-grid" style={GRID_STYLE}>
            {itemList.itemIds.map((id) => (
              <div key={id} data-item-id={id} {...tutorialAnchor(recipeItemAnchorId(id))}>
                <ItemSlot itemId={id} count={craftableCounts.get(id) ?? 0} catalog />
              </div>
            ))}
          </SlotGrid>
        </ScrollArea>
      ) : (
        <ConnectingPlaceholder />
      )}
    </GamePanel>
  );
}
