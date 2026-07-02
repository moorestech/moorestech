import { Tooltip } from "@mantine/core";
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
    <Tooltip label={fluid.name} disabled={!hasFluid || !fluid.name}>
      <div data-testid="fluid-slot" className={styles.slot}>
        {hasFluid ? (
          <>
            {/* amount/capacity に応じた下からの縦フィル */}
            {/* Vertical fill rising from the bottom by amount/capacity */}
            <div
              className={styles.fill}
              style={{ height: `${fillRatio(fluid.amount, fluid.capacity) * 100}%`, backgroundColor: fluidColor(fluid.fluidId) }}
            />
            <span className={styles.amount}>{formatAmount(fluid.amount)}</span>
          </>
        ) : null}
      </div>
    </Tooltip>
  );
}
