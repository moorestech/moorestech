import { Stack } from "@mantine/core";
import type { BlockInventoryOpen } from "@/bridge/contract/payloadTypes";
import BlockItemGrid from "../BlockItemGrid";
import MinerSection from "../details/MinerSection";
import GearSection from "../details/GearSection";
import { GearNetworkSection } from "../details/NetworkSections";

// ギア採掘機ビュー: uGUI MinerBlockInventoryView と SetGearText 系表示を組み合わせる
// Gear miner view: combines uGUI MinerBlockInventoryView with SetGearText-style gear info
export default function GearMinerInventory({ data }: { data: BlockInventoryOpen }) {
  return (
    <Stack gap="sm">
      <BlockItemGrid itemSlots={data.itemSlots} testId="gear-miner-output-grid" />
      <MinerSection data={data} />
      <GearSection data={data} />
      <GearNetworkSection data={data} />
    </Stack>
  );
}
