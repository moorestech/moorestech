import type { BlockInventoryData } from "@/bridge/payloadTypes";
import BlockItemGrid from "./BlockItemGrid";

// 未登録 blockType 用フォールバック。itemSlots を汎用的に描画し UI 不在のクラッシュを防ぐ
// Fallback for unregistered blockTypes; renders itemSlots generically to avoid a missing-UI crash
export default function GenericBlockInventory({ data }: { data: BlockInventoryData }) {
  return <BlockItemGrid itemSlots={data.itemSlots} testId="generic-block-grid" />;
}
