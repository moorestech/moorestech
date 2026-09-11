import { useCallback, useState } from "react";
import HoverTooltip from "../HoverTooltip";
import FluidIcon from "../FluidIcon";
import { type IconFallback, type IconRenderResult } from "../GameIcon";
import SlotFrame from "../SlotFrame";
import { formatSlotAmount } from "../slotAmountFormat";
import { fluidNameKey, useI18n } from "@/shared/i18n";
import styles from "./style.module.css";

// 量を出すか否かを型で表す。出さない側は量を持たない（前例 RecipeRow の ActionProps）
// The badge states in the type whether an amount shows; the hidden side carries none (precedent: RecipeRow's ActionProps)
export type AmountBadge =
  // 1回あたり量。0も0と描く
  // Amount per process; zero draws as 0
  | { kind: "show"; amount: number }
  | { kind: "hidden" };

type Props = {
  fluidGuid: string;
  badge: AmountBadge;
  testId?: string;
};

// 容量無し液体1マス。白面でアイテムと同枠（ADR0054）
// One capacity-less fluid cell sharing the item frame's white face (ADR0054)
export default function FluidAmountSlot({ fluidGuid, badge, testId }: Props) {
  const { resolveTranslation } = useI18n();
  const [iconRenderResult, setIconRenderResult] = useState<IconRenderResult>("image");
  const handleIconRenderResult = useCallback((result: IconRenderResult) => setIconRenderResult(result), []);

  // 名前は解決できた時だけ持つ。未解決の[!key]プレースホルダを液体名として描かない
  // A name exists only when it resolved, so an unresolved [!key] placeholder never poses as the fluid's name
  const nameTranslation = resolveTranslation(fluidNameKey(fluidGuid), {});
  const nameResolved = nameTranslation.kind === "resolved";
  const name = nameResolved ? nameTranslation.text : "";
  // 背面フィルの無い白面なのでアイコン失敗時は辞書の液体名を残す。液体のidは36文字GUIDでマスに収まらない
  // The white face carries no fill behind it, so a failed icon leaves the dictionary name; a fluid id is a 36-char GUID that no cell can hold
  const iconFallback: IconFallback = nameResolved ? { kind: "label", text: name } : { kind: "none" };
  // 白面の「中身あり」はアイコンが実際に描いたものと量バッジの有無だけから決める
  // The white face claims content only from what the icon actually drew and whether the amount badge shows
  const filled = iconRenderResult !== "empty" || badge.kind === "show";
  return (
    <HoverTooltip label={name} disabled={!nameResolved}>
      <SlotFrame testId={testId} filled={filled}>
        <FluidIcon fluidGuid={fluidGuid} fallback={iconFallback} onRenderResult={handleIconRenderResult} className={styles.icon} />
        {badge.kind === "show" ? (
          <span className={`iconTextOutlineLight ${styles.amount}`} data-testid={testId ? `${testId}-amount` : undefined}>
            {formatSlotAmount(badge.amount)}
          </span>
        ) : null}
      </SlotFrame>
    </HoverTooltip>
  );
}
