import { useMemo } from "react";
import type { CSSProperties } from "react";
import { ScrollArea, Text } from "@mantine/core";
import { useTopic, Topics } from "@/bridge";
import { ItemSlot, SlotGrid, GamePanel } from "@/shared/ui";
import { useItemSelectionStore } from "./selectionStore";
import styles from "./ItemListPanel.module.css";

// 固定pxで6列のピッチを均一化する
// Use fixed-pixel tracks and gaps to keep all six catalog columns on a uniform 140px screenshot pitch
// カタログは2.5/3/3.5/4/4.5pxでrec-whiteまたは列検出が崩れたため、両方を守る実測下限5pxを使う
// Use the measured 5px catalog floor because 2.5/3/3.5/4/4.5px broke rec-white or column detection
const GRID_STYLE = { "--slot-size": "46.144px", "--slot-grid-gap": "8.656px", "--slot-grid-row-gap": "8.896px", "--icon-pad": "5px", "--count-bottom": "-1px", "--count-font-size": "16px", "--count-letter-spacing": "0.12em" } as CSSProperties;

// 右カラム: 表示対象アイテムの一覧（uGUI の ItemListView 準拠）。クリックで中央にレシピ表示
// Right column: list of viewable items, like uGUI's ItemListView; click shows recipes in the center
export default function ItemListPanel() {
  const onSelect = useItemSelectionStore((s) => s.setSelectedItem);
  const itemList = useTopic(Topics.itemList);
  const inventory = useTopic(Topics.inventory);

  // uGUI 同様、所持中のアイテムだけ白面＋個数で強調する。所持数は main+hotbar を合算
  // Like uGUI, only owned items get a white face + count; owned totals sum main+hotbar
  const ownedCounts = useMemo(() => {
    const counts = new Map<number, number>();
    if (!inventory) return counts;
    for (const slot of [...inventory.mainSlots, ...inventory.hotbarSlots]) {
      if (slot.itemId > 0 && slot.count > 0) counts.set(slot.itemId, (counts.get(slot.itemId) ?? 0) + slot.count);
    }
    return counts;
  }, [inventory]);

  return (
    <GamePanel
      gridArea="items"
      title="CRAFT RECIPE"
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
        >
          <SlotGrid cols={6} testId="item-list-grid" style={GRID_STYLE}>
            {itemList.itemIds.map((id) => (
              <ItemSlot
                key={id}
                itemId={id}
                count={ownedCounts.get(id)}
                catalog
                onLeftDown={() => onSelect(id)}
              />
            ))}
          </SlotGrid>
        </ScrollArea.Autosize>
      ) : (
        <Text size="sm" c="dimmed">connecting...</Text>
      )}
    </GamePanel>
  );
}
