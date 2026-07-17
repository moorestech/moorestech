import { useMemo } from "react";
import type { CSSProperties } from "react";
import { ScrollArea, Text } from "@mantine/core";
import { useTopic, Topics, useItemMaster } from "@/bridge";
import { ItemSlot, SlotGrid, GamePanel } from "@/shared/ui";
import { useItemSelectionStore } from "./selectionStore";
import styles from "./ItemListPanel.module.css";

// 正本のスロット外形123px・間隔16pxへ寄せる局所上書き（持ち物と共通、他グリッドの既定値は変えない）
// Local override toward the reference's 123px slot face / 16px gap (shared with inventory; other grids keep their defaults)
// iter6: 正本の面-間隔断面実測(y500)はギャップ計28screenshot-px(8pxリング×2+パネル色12px可視)。旧設定は
// 面が8px screenshot分(≈3.13CSSpx)太すぎパネル色が2pxしか覗かない。ピッチ(size+gap合計)は不変のまま
// 面を3.13px縮めgapへ同量足し、面の左端(recipe-grid-col2アンカー)を動かさず断面だけ正本へ寄せる
// iter6: The reference's face-gap cross-section (y500) measures a 28-screenshot-px gap (8px ring ×2 +
// 12px visible panel color). The old split ran the face ~3.13 CSS px (8 screenshot px) too wide, leaving
// only 2px of panel color visible. Pitch (size+gap sum) stays fixed; shave 3.13px off the face and add it
// to the gap so the cross-section matches the reference without moving the face's left edge (the
// recipe-grid-col2 anchor)
const GRID_STYLE = { "--slot-size": "2.884rem", "--slot-grid-gap": "0.556rem" } as CSSProperties;

// 右カラム: 表示対象アイテムの一覧（uGUI の ItemListView 準拠）。クリックで中央にレシピ表示
// Right column: list of viewable items, like uGUI's ItemListView; click shows recipes in the center
export default function ItemListPanel() {
  const onSelect = useItemSelectionStore((s) => s.setSelectedItem);
  const itemList = useTopic(Topics.itemList);
  const inventory = useTopic(Topics.inventory);
  const itemMaster = useItemMaster();

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
      style={{ justifySelf: "end", alignSelf: "start", width: 378, minHeight: 452 }}
    >
      {itemList ? (
        // mahは7段が丸ごと収まりつつDEMO60件(10段)でノブ比が正本≈70%になる高さ。marginLeftはグリッド内側
        // インデント補正、marginTopはノブの縦位置合わせ。align-self:stretchだとmarginLeftだけでは右端(ノブ位置)が
        // 動かないためmarginRightで右端を別途詰める
        // mah fits 7 full rows while making the DEMO 60-item (10-row) thumb ratio match the reference ~70%. marginLeft
        // corrects the grid inset; marginTop aligns the knob vertically. Under align-self:stretch, marginLeft alone
        // doesn't move the right edge (knob position), so marginRight tucks the right edge in separately
        <ScrollArea.Autosize
          mah={382}
          type="always"
          scrollbarSize={4}
          className={styles.scrollArea}
          // iter6: 中央craft-panelの幅を0.8CSSpx縮めた分、grid-template-columns:autoの列境界が
          // 連動して左へずれ、justifySelf:end中の本グリッドも巻き込まれてrecipe-grid-col2が後退した。
          // marginLeftを+1.57px戻して打ち消す
          // iter6: Trimming the center craft-panel's width by 0.8 CSS px shifted the auto column boundary
          // left, dragging this justifySelf:end grid along with it and regressing recipe-grid-col2; add
          // back +1.57px of marginLeft to cancel it out
          style={{ marginLeft: -0.43, marginRight: 6, marginTop: 12 }}
        >
          <SlotGrid cols={6} testId="item-list-grid" style={GRID_STYLE}>
            {itemList.itemIds.map((id) => (
              <ItemSlot
                key={id}
                itemId={id}
                count={ownedCounts.get(id)}
                catalog
                name={itemMaster?.get(id)?.name}
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
