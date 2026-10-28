import { Stack, Text } from "@mantine/core";
import type { BlockInventoryOpen } from "@/bridge";
import { L, useI18n } from "@/shared/i18n";
import BlockItemGrid from "../BlockItemGrid";
import { ElectricNetworkSection } from "../details/NetworkSections";
import { toSpaceElevatorDetailState } from "../details/spaceElevator/spaceElevatorDetailState";
import SpaceElevatorSection from "../details/spaceElevator/SpaceElevatorSection";

// 入出力を分けても、機械UIと同じく元のサーバースロットindexで操作する
// Like the machine UI, keep server slot indices when separating the inlet and outlets
export default function SpaceElevatorInventory({ data }: { data: BlockInventoryOpen }) {
  const { t } = useI18n();
  const detail = data.spaceElevator;
  const groups = detail ? [
    { label: L.ui.blockInventory.spaceElevatorSatelliteInput, testId: "space-elevator-input-grid", indices: detail.satelliteInputSlots },
    { label: L.ui.blockInventory.spaceElevatorResultOutput, testId: "space-elevator-output-grid", indices: detail.outputSlots },
  ] : [];

  return (
    <Stack gap="sm" data-testid="space-elevator-view">
      <Stack gap="sm" data-testid="space-elevator-grid">
        {groups.map((group) => (
          <Stack key={group.testId} gap={4}>
            <Text size="sm" c="var(--text-muted)">{t(group.label)}</Text>
            <BlockItemGrid itemSlots={data.itemSlots} slotIndices={group.indices} testId={group.testId} />
          </Stack>
        ))}
      </Stack>
      <SpaceElevatorSection state={toSpaceElevatorDetailState(data)} />
      <ElectricNetworkSection data={data} />
    </Stack>
  );
}
