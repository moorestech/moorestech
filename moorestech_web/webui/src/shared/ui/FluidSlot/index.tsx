import { Tooltip } from "@mantine/core";
import type { FluidSlotData } from "@/bridge";
import { fluidNameKey, useI18n } from "@/shared/i18n";
import FluidIcon from "../FluidIcon";
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
  const { t } = useI18n();
  const hasFluid = fluid.fluidId > 0 && fluid.amount > 0;
  // 空流体はguidが空文字なので辞書解決せずラベルも出さない
  // The empty fluid carries an empty guid, so skip dictionary lookup and show no label
  const name = fluid.fluidGuid ? t(fluidNameKey(fluid.fluidGuid)) : "";

  return (
    <Tooltip label={name} disabled={!hasFluid || !name}>
      <div data-testid="fluid-slot" className={styles.slot}>
        {hasFluid ? (
          <>
            {/* amount/capacity に応じた下からの縦フィル */}
            {/* Vertical fill rising from the bottom by amount/capacity */}
            <div
              className={styles.fill}
              style={{ height: `${fillRatio(fluid.amount, fluid.capacity) * 100}%`, backgroundColor: fluidColor(fluid.fluidId) }}
            />
            {/* フィルの上に実アイコンを重ね、量バッジは最前面に残す */}
            {/* Layer the real icon over the fill, keeping the amount badge frontmost */}
            {fluid.fluidGuid ? <FluidIcon fluidGuid={fluid.fluidGuid} className={styles.icon} /> : null}
            <span className={styles.amount}>{formatAmount(fluid.amount)}</span>
          </>
        ) : null}
      </div>
    </Tooltip>
  );
}
