import { Stack } from "@mantine/core";
import type { BlockInventoryOpen } from "@/bridge";
import { L } from "@/shared/i18n";
import { BlockItemGridGroup } from "../BlockItemGrid";
import { ElectricNetworkSection } from "../details/NetworkSections";
import { toSpaceElevatorDetailState } from "../details/spaceElevator/spaceElevatorDetailState";
import SpaceElevatorSection from "../details/spaceElevator/SpaceElevatorSection";

// 入出力を分けても、機械UIと同じく元のサーバースロットindexで操作する
// Like the machine UI, keep server slot indices when separating the inlet and outlets
export default function SpaceElevatorInventory({ data }: { data: BlockInventoryOpen }) {
  const detail = data.spaceElevator;
  const groups = detail ? [
    { labelKey: L.ui.blockInventory.spaceElevatorSatelliteInput, testId: "space-elevator-input-grid", slotIndices: detail.satelliteInputSlots },
    { labelKey: L.ui.blockInventory.spaceElevatorResultOutput, testId: "space-elevator-output-grid", slotIndices: detail.outputSlots },
  ] : [];

  return (
    <Stack gap="sm" data-testid="space-elevator-view">
      <Stack gap="sm" data-testid="space-elevator-grid">
        {groups.map((group) => <BlockItemGridGroup key={group.testId} itemSlots={data.itemSlots} {...group} />)}
      </Stack>
      <SpaceElevatorSection state={toSpaceElevatorDetailState(data)} />
      <ElectricNetworkSection data={data} />
    </Stack>
  );
}
