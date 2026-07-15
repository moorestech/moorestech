import { ScrollArea, Text } from "@mantine/core";
import { useTopic, Topics, useItemMaster } from "@/bridge";
import { ItemSlot, SlotGrid, GamePanel } from "@/shared/ui";
import { useItemSelectionStore } from "./selectionStore";

// 右カラム: 表示対象アイテムの一覧（uGUI の ItemListView 準拠）。クリックで中央にレシピ表示
// Right column: list of viewable items, like uGUI's ItemListView; click shows recipes in the center
export default function ItemListPanel() {
  const selectedItemId = useItemSelectionStore((s) => s.selectedItemId);
  const onSelect = useItemSelectionStore((s) => s.setSelectedItem);
  const itemList = useTopic(Topics.itemList);
  const itemMaster = useItemMaster();

  return (
    <GamePanel gridArea="items" title="CRAFT RECIPE" style={{ justifySelf: "end" }}>
      {itemList ? (
        <ScrollArea.Autosize mah="60vh" type="auto" offsetScrollbars>
          <SlotGrid cols={6} testId="item-list-grid">
            {itemList.itemIds.map((id) => (
              <ItemSlot
                key={id}
                itemId={id}
                name={itemMaster?.get(id)?.name}
                selected={id === selectedItemId}
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
