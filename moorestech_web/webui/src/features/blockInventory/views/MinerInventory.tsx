import { Stack } from "@mantine/core";
import type { BlockInventoryOpen } from "@/bridge";
import BlockItemGrid from "../BlockItemGrid";
import MinerSection from "../details/MinerSection";
import { ElectricNetworkSection } from "../details/NetworkSections";

// 電気採掘機ビュー: uGUI MinerBlockInventoryView と出力スロットを組み合わせる
// Electric miner view: combines uGUI MinerBlockInventoryView with output slots
export default function MinerInventory({ data }: { data: BlockInventoryOpen }) {
  return (
    <Stack gap="sm">
      <BlockItemGrid itemSlots={data.itemSlots} testId="miner-output-grid" />
      <MinerSection data={data} />
      <ElectricNetworkSection data={data} />
    </Stack>
  );
}
