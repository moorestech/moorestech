import { Stack } from "@mantine/core";
import type { BlockInventoryOpen } from "@/bridge";
import MachineSection from "../details/MachineSection";
import { ElectricNetworkSection } from "../details/NetworkSections";

// 電気機械ビュー: uGUI MachineBlockInventoryView と ElectricNetworkInfoView を組み合わせる
// Electric machine view: combines uGUI MachineBlockInventoryView with ElectricNetworkInfoView
export default function MachineInventory({ data }: { data: BlockInventoryOpen }) {
  return (
    <Stack gap="sm">
      <MachineSection data={data} />
      <ElectricNetworkSection data={data} />
    </Stack>
  );
}
