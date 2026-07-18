import type { BlockInventoryOpen } from "@/bridge";
import { FluidSlotRow } from "@/shared/ui";

// Tank UI: uGUI 流体タンク同様 fluidSlots を列展開＋進捗矢印
// Tank UI: mirrors uGUI fluid tank; fluidSlots row plus a progress arrow
export default function TankInventory({ data }: { data: BlockInventoryOpen }) {
  return <FluidSlotRow fluids={data.fluidSlots} progress={data.progress} testId="tank-body" />;
}
