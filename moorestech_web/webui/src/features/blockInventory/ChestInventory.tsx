import type { BlockInventoryOpen } from "@/bridge/payloadTypes";
import BlockItemGrid from "./BlockItemGrid";

// Chest UI: uGUI ChestBlockInventoryView 同様 itemSlots をグリッド展開
// Chest UI: mirrors uGUI ChestBlockInventoryView; itemSlots as a grid
export default function ChestInventory({ data }: { data: BlockInventoryOpen }) {
  return <BlockItemGrid itemSlots={data.itemSlots} testId="chest-grid" />;
}
