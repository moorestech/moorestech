import { Button, Group, Stack, Text, Title } from "@mantine/core";
import { useTopic, dispatchAction, Topics, useItemMaster } from "@/bridge";
import { ItemSlot, SlotGrid } from "@/shared/ui";
import type { SlotRef } from "@/bridge/contract/payloadTypes";
import { createSlotActions } from "../slotActions";
import GrabOverlay from "./GrabOverlay";

// プレイヤーインベントリ（メイン4行+Sort+grab）の表示と操作。ホットバーは常時表示の HotbarPanel が担う
// Player inventory view & interactions: 4 main rows, Sort, and the grab stack; the hotbar lives in the always-on HotbarPanel
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
    <>
      <Stack gap="sm" style={{ gridArea: "inv" }}>
        <Group gap="sm">
          <Title order={2} size="h4">Inventory</Title>
          <Button variant="default" size="compact-sm" onClick={() => void dispatchAction("inventory.sort", {})}>
            Sort
          </Button>
        </Group>
        <SlotGrid testId="main-grid">
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
      </Stack>
      <GrabOverlay grab={inventory.grab} />
    </>
  );
}
