import { Stack } from "@mantine/core";
import type { BlockInventoryOpen } from "@/bridge/payloadTypes";
import BlockItemGrid from "../BlockItemGrid";
import GeneratorSection from "../details/GeneratorSection";
import GearSection from "../details/GearSection";
import { ElectricNetworkSection, GearNetworkSection } from "../details/NetworkSections";

// 発電機共通ビュー: 電気発電機とギア発電機の uGUI 表示を併置する
// Shared generator view: places electric and gear generator uGUI-style sections together
export default function GeneratorInventory({ data }: { data: BlockInventoryOpen }) {
  return (
    <Stack gap="sm">
      <BlockItemGrid itemSlots={data.itemSlots} testId="generator-fuel-grid" />
      <GeneratorSection data={data} />
      <GearSection data={data} />
      <ElectricNetworkSection data={data} />
      <GearNetworkSection data={data} />
    </Stack>
  );
}
