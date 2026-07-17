import { Text } from "@mantine/core";
import { useTopic, Topics, useItemMaster } from "@/bridge";
import { ItemSlot, SlotGrid, GamePanel } from "@/shared/ui";
import type { SlotRef } from "@/bridge/contract/payloadTypes";
import { createSlotActions } from "../slotActions";

// メイン4行を操作する。grab追従とホットバーは常時別表示
// Handle four main rows; grab tracking and the hotbar render separately
export default function InventoryPanel() {
  const inventory = useTopic(Topics.inventory);
  const itemMaster = useItemMaster();

  if (!inventory) {
    return <Text size="sm" c="dimmed" style={{ gridArea: "inv" }}>connecting...</Text>;
  }

  // クリック操作は HotbarPanel と共通のファクトリで生成する
  // Click interactions come from the factory shared with HotbarPanel
  const actions = createSlotActions(inventory, itemMaster);

  return (
    <GamePanel gridArea="inv" title="持ち物" style={{ justifySelf: "start", alignSelf: "start", width: 378, minHeight: 452 }}>
      <SlotGrid testId="main-grid" cols={6}>
        {inventory.mainSlots.map((slot, i) => {
          const ref: SlotRef = { area: "main", slot: i };
          return (
            <ItemSlot
              key={`main-${i}`}
              itemId={slot.itemId}
              count={slot.count}
              name={itemMaster?.get(slot.itemId)?.name}
              onLeftDown={(shiftKey) => actions.onLeftDown(ref, slot, shiftKey)}
              onRightDown={() => actions.onRightDown(ref, slot)}
              onDoubleClick={() => actions.onDoubleClick(ref)}
            />
          );
        })}
      </SlotGrid>
    </GamePanel>
  );
}
