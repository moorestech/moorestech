import type { FluidSlotData } from "@/bridge/payloadTypes";
import { formatAmount, fillRatio } from "./fluidLogic";
import styles from "./style.module.css";

// fluidId から決定的に色相を導き、液体ごとに安定した色を割り当てる（アイコン代替）
// Derive a deterministic hue from fluidId so each fluid gets a stable color (icon substitute)
function fluidColor(fluidId: number): string {
  const hue = (fluidId * 47) % 360;
  return `hsl(${hue}, 70%, 45%)`;
}

// 色ボックス/量/ホバー名を持つ汎用流体スロット。uGUI FluidSlotView 相当
// Generic fluid slot (color box, amount, hover name); mirrors uGUI FluidSlotView
export default function FluidSlot({ fluid }: { fluid: FluidSlotData }) {
  const hasFluid = fluid.fluidId > 0 && fluid.amount > 0;

  return (
    <div
      data-testid="fluid-slot"
      className="group relative w-12 h-12 border border-gray-700 rounded bg-gray-900 overflow-hidden select-none"
    >
      {hasFluid ? (
        <>
          {/* amount/capacity に応じた下からの縦フィル */}
          {/* Vertical fill rising from the bottom by amount/capacity */}
          <div
            className="absolute bottom-0 left-0 w-full"
            style={{ height: `${fillRatio(fluid.amount, fluid.capacity) * 100}%`, backgroundColor: fluidColor(fluid.fluidId) }}
          />
          {/* 量テキストは ItemSlot の count と同じく右下に小さく表示 */}
          {/* Amount text shown small at bottom-right like ItemSlot's count */}
          <span className="absolute bottom-0 right-0.5 text-xs text-white font-bold drop-shadow">
            {formatAmount(fluid.amount)}
          </span>
          {fluid.name ? (
            <span className={`pointer-events-none hidden group-hover:block bg-black/90 text-white text-xs rounded px-2 py-1 ${styles.tooltip}`}>
              {fluid.name}
            </span>
          ) : null}
        </>
      ) : null}
    </div>
  );
}
