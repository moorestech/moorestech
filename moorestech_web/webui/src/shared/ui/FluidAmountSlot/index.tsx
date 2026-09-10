import HoverTooltip from "../HoverTooltip";
import FluidIcon from "../FluidIcon";
import SlotFrame from "../SlotFrame";
import { formatAmount } from "../FluidSlot/fluidLogic";
import { fluidNameKey, useI18n } from "@/shared/i18n";
import styles from "./style.module.css";

type Props = {
  fluidGuid: string;
  // レシピ量（加工1回あたりの必要量/生産量）。未指定ならバッジを出さない
  // Recipe amount (per-process input/output); no badge when omitted
  amount?: number;
  testId?: string;
};

// レシピ行・選択中レシピ表示向けの液体1マス。容量の概念が無いため充填フィルは持たず、面はアイテムと同じ白面（ADR 0054）
// One fluid cell for recipe rows and the selected-recipe header; no capacity so no fill, and the face is the same white as items (ADR 0054)
export default function FluidAmountSlot({ fluidGuid, amount, testId }: Props) {
  const { t } = useI18n();
  const name = t(fluidNameKey(fluidGuid));
  return (
    <HoverTooltip label={name} disabled={!name}>
      <SlotFrame testId={testId} filled>
        <FluidIcon fluidGuid={fluidGuid} className={styles.icon} />
        {amount !== undefined && amount > 0 ? <span className={`iconTextOutlineLight ${styles.amount}`}>{formatAmount(amount)}</span> : null}
      </SlotFrame>
    </HoverTooltip>
  );
}
