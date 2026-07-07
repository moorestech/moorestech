import { Stack } from "@mantine/core";
import type { BlockInventoryOpen } from "@/bridge/contract/payloadTypes";
import { FluidSlotRow } from "@/shared/ui";
import BlockItemGrid from "../BlockItemGrid";

// 未登録 blockType 用フォールバック。item/fluid/progress を過不足なく描画しクラッシュを防ぐ
// Fallback for unregistered blockTypes; renders items/fluids/progress so no data is lost and nothing crashes
export default function GenericBlockInventory({ data }: { data: BlockInventoryOpen }) {
  return (
    <Stack gap="sm">
      {/* アイテムスロットがあればグリッド描画 */}
      {/* Render the item slots as a grid when present */}
      {data.itemSlots.length > 0 ? (
        <BlockItemGrid itemSlots={data.itemSlots} testId="generic-block-grid" />
      ) : null}
      {/* 流体スロットは共通の FluidSlotRow（空なら非描画） */}
      {/* Fluid slots via the shared FluidSlotRow (renders nothing when empty) */}
      <FluidSlotRow fluids={data.fluidSlots} progress={data.progress} testId="generic-block-fluids" />
    </Stack>
  );
}
