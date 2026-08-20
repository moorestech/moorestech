import { fluidNameKey, L, useI18n } from "@/shared/i18n";
import { formatAmount } from "@/shared/ui/FluidSlot/fluidLogic";
import styles from "../style.module.css";

type Props = { fluidGuid: string; amount: number };

// 解放レシピの液体出力は容量を持たないため、充填率を描くFluidSlotではなく量ラベルだけで示す
// A recipe's fluid output has no capacity, so show an amount label instead of FluidSlot's fill ratio
export default function UnlockFluidLabel({ fluidGuid, amount }: Props) {
  const { t } = useI18n();
  return (
    <span className={styles.unlockFluidAmount} data-testid="research-unlock-fluid">
      {t(L.ui.research.unlockFluidSummary, { fluidName: t(fluidNameKey(fluidGuid)), amount: formatAmount(amount) })}
    </span>
  );
}
