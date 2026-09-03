import HoverTooltip from "../HoverTooltip";
import { useFluidMaster, type FluidSlotData } from "@/bridge";
import { fluidNameKey, useI18n } from "@/shared/i18n";
import FluidIcon from "../FluidIcon";
import { formatAmount, fillRatio } from "./fluidLogic";
import styles from "./style.module.css";

// 背面フィル/量/ホバー名を持つ汎用流体スロット。uGUI FluidSlotView 相当
// Generic fluid slot (back fill, amount, hover name); mirrors uGUI FluidSlotView
export default function FluidSlot({ fluid }: { fluid: FluidSlotData }) {
  const { t } = useI18n();
  const fluidMaster = useFluidMaster();

  // 空スロットは名前もフィルも持たないのでツールチップごと省く
  // An empty slot has neither a name nor a fill, so drop the tooltip entirely
  if (fluid.kind === "empty") {
    return <div data-testid="fluid-slot" className={styles.slot} />;
  }

  const name = t(fluidNameKey(fluid.fluidGuid));
  const color = fluidMaster?.get(fluid.fluidGuid)?.color;

  return (
    <HoverTooltip label={name} disabled={!name}>
      <div data-testid="fluid-slot" className={styles.slot}>
        {/* マスタ色が未取得の間はフィルを描かない（フォールバック色でごまかさない） */}
        {/* No fill is drawn until the master color arrives (no fallback color) */}
        {color !== undefined && (
          <div
            className={styles.fill}
            style={{ height: `${fillRatio(fluid.amount, fluid.capacity) * 100}%`, backgroundColor: color }}
          />
        )}
        {/* フィルの上に実アイコンを重ね、量バッジは最前面に残す */}
        {/* Layer the real icon over the fill, keeping the amount badge frontmost */}
        <FluidIcon fluidGuid={fluid.fluidGuid} className={styles.icon} />
        <span className={`iconTextOutlineDark ${styles.amount}`}>{formatAmount(fluid.amount)}</span>
      </div>
    </HoverTooltip>
  );
}
