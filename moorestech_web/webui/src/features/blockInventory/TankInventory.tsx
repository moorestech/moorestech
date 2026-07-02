import { Group } from "@mantine/core";
import type { BlockInventoryData } from "@/bridge/payloadTypes";
import { FluidSlot, ProgressArrow } from "@/shared/ui";

// Tank UI: uGUI 流体タンク同様 fluidSlots を列展開＋進捗矢印
// Tank UI: mirrors uGUI fluid tank; fluidSlots row plus a progress arrow
export default function TankInventory({ data }: { data: BlockInventoryData }) {
  return (
    <Group data-testid="tank-body" gap="xs" align="center">
      {/* 各流体スロットを横並びで描画 */}
      {/* Render each fluid slot in a row */}
      {data.fluidSlots.map((fluid, i) => (
        <FluidSlot key={i} fluid={fluid} />
      ))}
      {/* progress が非 null のときだけ加工進捗の矢印を表示 */}
      {/* Show the processing progress arrow only when progress is non-null */}
      {data.progress != null ? <ProgressArrow value={data.progress} /> : null}
    </Group>
  );
}
