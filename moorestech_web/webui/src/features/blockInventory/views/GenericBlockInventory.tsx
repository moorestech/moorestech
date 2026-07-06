import { Group, Stack } from "@mantine/core";
import type { BlockInventoryOpen } from "@/bridge/payloadTypes";
import { FluidSlot, ProgressArrow } from "@/shared/ui";
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
      {/* 流体スロットがあれば横並び＋進捗矢印で描画 */}
      {/* Render fluid slots in a row plus a progress arrow when present */}
      {data.fluidSlots.length > 0 ? (
        <Group data-testid="generic-block-fluids" gap="xs" align="center">
          {data.fluidSlots.map((fluid, i) => (
            <FluidSlot key={i} fluid={fluid} />
          ))}
          {data.progress != null ? <ProgressArrow value={data.progress} /> : null}
        </Group>
      ) : null}
    </Stack>
  );
}
