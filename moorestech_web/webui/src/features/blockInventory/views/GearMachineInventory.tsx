import { Stack } from "@mantine/core";
import type { BlockInventoryOpen } from "@/bridge/payloadTypes";
import MachineSection from "../details/MachineSection";
import GearSection from "../details/GearSection";
import { GearNetworkSection } from "../details/NetworkSections";

// ギア機械ビュー: uGUI MachineBlockInventoryView と SetGearText 系表示を組み合わせる
// Gear machine view: combines uGUI MachineBlockInventoryView with SetGearText-style gear info
export default function GearMachineInventory({ data }: { data: BlockInventoryOpen }) {
  return (
    <Stack gap="sm">
      <MachineSection data={data} />
      <GearSection data={data} />
      <GearNetworkSection data={data} />
    </Stack>
  );
}
