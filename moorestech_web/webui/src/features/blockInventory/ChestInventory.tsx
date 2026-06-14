import type { BlockInventoryData } from "@/bridge/payloadTypes";
import BlockItemGrid from "./BlockItemGrid";

// Chest UI: uGUI ChestBlockInventoryView 同様 itemSlots をグリッド展開
// Chest UI: mirrors uGUI ChestBlockInventoryView; itemSlots as a grid
export default function ChestInventory({ data }: { data: BlockInventoryData }) {
  return <BlockItemGrid itemSlots={data.itemSlots} testId="chest-grid" />;
}
