import type { BlockInventoryData } from "@/bridge/payloadTypes";
import { FluidSlot, ProgressArrow } from "@/shared/ui";

// Tank UI: uGUI の流体タンクビュー同様、fluidSlots を流体スロット列へ展開し進捗矢印を併置
// Tank UI: mirrors the uGUI fluid tank view, laying fluidSlots out as a row with a progress arrow
export default function TankInventory({ data }: { data: BlockInventoryData }) {
  return (
    <div data-testid="tank-body" className="flex items-center gap-2">
      {/* 各流体スロットを横並びで描画 */}
      {/* Render each fluid slot in a row */}
      {data.fluidSlots.map((fluid, i) => (
        <FluidSlot key={i} fluid={fluid} />
      ))}
      {/* progress が非 null のときだけ加工進捗の矢印を表示 */}
      {/* Show the processing progress arrow only when progress is non-null */}
      {data.progress != null ? <ProgressArrow value={data.progress} /> : null}
    </div>
  );
}
