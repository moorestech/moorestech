import { useMemo } from "react";
import { ScrollArea, Text } from "@mantine/core";
import { useTopic, Topics, useItemMaster } from "@/bridge";
import { ItemSlot, SlotGrid, GamePanel } from "@/shared/ui";
import { useItemSelectionStore } from "./selectionStore";
import styles from "./ItemListPanel.module.css";

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
    <GamePanel gridArea="items" title="CRAFT RECIPE" style={{ justifySelf: "end", alignSelf: "start", width: 378, minHeight: 452 }}>
      {itemList ? (
        // mah は正本のノブ寸法(トラック比≈70%)に寄せるため7段全高より小さくする。marginLeftはグリッド内側インデント補正、
        // marginTopはノブの縦位置合わせ。align-self:stretchだとmarginLeftだけでは右端(ノブ位置)が動かないため
        // marginRightで右端を別途詰める
        // mah is kept below the full 7-row height so the thumb ratio approximates the reference (~70%). marginLeft
        // corrects the grid inset; marginTop aligns the knob vertically. Under align-self:stretch, marginLeft alone
        // doesn't move the right edge (knob position), so marginRight tucks the right edge in separately
        <ScrollArea.Autosize
          mah={319}
          type="always"
          scrollbarSize={4}
          className={styles.scrollArea}
          style={{ marginLeft: -14, marginRight: 6, marginTop: 12 }}
        >
          <SlotGrid cols={6} testId="item-list-grid">
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
