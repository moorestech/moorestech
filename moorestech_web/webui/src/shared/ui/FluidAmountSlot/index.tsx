import HoverTooltip from "../HoverTooltip";
import FluidIcon from "../FluidIcon";
import { type IconFallback } from "../GameIcon";
import SlotFrame from "../SlotFrame";
import { formatSlotAmount } from "../slotAmountFormat";
import { fluidNameKey, useI18n } from "@/shared/i18n";
import styles from "./style.module.css";

type Props = {
  fluidGuid: string;
  // 1回あたり量。0も0と描き、出すか否かはshowAmountだけが決める
  // Amount per process; zero draws as 0 and only showAmount decides whether it appears
  amount: number;
  showAmount: boolean;
  testId?: string;
};

// 容量無し液体1マス。白面でアイテムと同枠（ADR0054）
// One capacity-less fluid cell sharing the item frame's white face (ADR0054)
export default function FluidAmountSlot({ fluidGuid, amount, showAmount, testId }: Props) {
  const { t } = useI18n();
  const name = t(fluidNameKey(fluidGuid));
  // 背面フィルの無い白面なのでアイコン失敗時は辞書の液体名を残す。液体のidは36文字GUIDでマスに収まらない
  // The white face carries no fill behind it, so a failed icon leaves the dictionary name; a fluid id is a 36-char GUID that no cell can hold
  const iconFallback: IconFallback = name ? { kind: "label", text: name } : { kind: "none" };
  // 描けるのはアイコン（液体名フォールバック込み）か量バッジ。どちらも無い枠を白面で「中身あり」と主張しない
  // Only the icon (name fallback included) or the amount badge can draw; a frame with neither never claims content through the white face
  const filled = iconFallback.kind !== "none" || showAmount;
  return (
    <HoverTooltip label={name} disabled={!name}>
      <SlotFrame testId={testId} filled={filled}>
        <FluidIcon fluidGuid={fluidGuid} fallback={iconFallback} className={styles.icon} />
        {showAmount ? (
          <span className={`iconTextOutlineLight ${styles.amount}`} data-testid={testId ? `${testId}-amount` : undefined}>
            {formatSlotAmount(amount)}
          </span>
        ) : null}
      </SlotFrame>
    </HoverTooltip>
  );
}
