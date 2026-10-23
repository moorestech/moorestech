import { Stack, Text } from "@mantine/core";
import type { BlockInventoryOpen } from "@/bridge";
import { L, useI18n } from "@/shared/i18n";
import { ItemSlot, SlotGrid } from "@/shared/ui";
import { useBlockSlotGestures } from "../useBlockSlotGestures";
import { ElectricNetworkSection } from "../details/NetworkSections";
import SpaceElevatorSection from "../details/spaceElevator/SpaceElevatorSection";

// 入出力を分けても、機械UIと同じく元のサーバースロットindexで操作する
// Like the machine UI, keep server slot indices when separating the inlet and outlets
export default function SpaceElevatorInventory({ data }: { data: BlockInventoryOpen }) {
  const { t } = useI18n();
  const gestures = useBlockSlotGestures();
  const indices = data.itemSlots.map((_, index) => index);
  const groups = [
    { label: L.ui.blockInventory.spaceElevatorSatelliteInput, testId: "space-elevator-input-grid", indices: indices.slice(0, 1) },
    { label: L.ui.blockInventory.spaceElevatorResultOutput, testId: "space-elevator-output-grid", indices: indices.slice(1) },
  ];

  return (
    <Stack gap="sm" data-testid="space-elevator-view">
      <Stack gap="sm" data-testid="space-elevator-grid">
        {groups.map((group) => (
          <Stack key={group.testId} gap={4}>
            <Text size="sm" c="var(--text-muted)">{t(group.label)}</Text>
            <SlotGrid cols={Math.min(9, Math.max(1, group.indices.length))} testId={group.testId}>
              {group.indices.map((index) => (
                <ItemSlot
                  key={index}
                  itemId={data.itemSlots[index].itemId}
                  count={data.itemSlots[index].count}
                  onLeftDown={(shiftKey) => gestures.onLeftDown(index, shiftKey)}
                  onRightDown={() => gestures.onRightDown(index)}
                  onDoubleClick={() => gestures.onDoubleClick(index)}
                />
              ))}
            </SlotGrid>
          </Stack>
        ))}
      </Stack>
      <SpaceElevatorSection data={data} />
      <ElectricNetworkSection data={data} />
    </Stack>
  );
}
