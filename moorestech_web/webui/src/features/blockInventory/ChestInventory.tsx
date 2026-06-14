import type { BlockInventoryData } from "@/bridge/payloadTypes";
import BlockItemGrid from "./BlockItemGrid";

// Chest UI: uGUI の ChestBlockInventoryView 同様、itemSlots をアイテムスロットのグリッドへ展開
// Chest UI: mirrors uGUI ChestBlockInventoryView, laying itemSlots out as a grid of item slots
export default function ChestInventory({ data }: { data: BlockInventoryData }) {
  return <BlockItemGrid itemSlots={data.itemSlots} testId="chest-grid" />;
}
