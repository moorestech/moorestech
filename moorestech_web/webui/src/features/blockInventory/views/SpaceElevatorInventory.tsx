import { Stack } from "@mantine/core";
import type { BlockInventoryOpen } from "@/bridge";
import BlockItemGrid from "../BlockItemGrid";
import { ElectricNetworkSection } from "../details/NetworkSections";
import SpaceElevatorSection from "../details/spaceElevator/SpaceElevatorSection";

const ITEM_SLOTS_TEST_ID = "space-elevator-grid";

// 衛星の入口と成果の出口、接続と昇降の固有部分、電力網を縦に並べる
// Stack the satellite inlet and result outlets, the connect/climb section and the power network
export default function SpaceElevatorInventory({ data }: { data: BlockInventoryOpen }) {
  return (
    <Stack gap="sm" data-testid="space-elevator-view">
      <BlockItemGrid itemSlots={data.itemSlots} testId={ITEM_SLOTS_TEST_ID} />
      <SpaceElevatorSection data={data} />
      <ElectricNetworkSection data={data} />
    </Stack>
  );
}
