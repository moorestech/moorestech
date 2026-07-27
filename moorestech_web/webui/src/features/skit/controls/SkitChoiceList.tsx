import { dispatchAction, type ActionPayloads, type SkitPresentationData } from "@/bridge";
import { useI18n } from "@/shared/i18n";
import { ChoiceMarkerIcon } from "../icons";
import styles from "./choices.module.css";

type Choice = SkitPresentationData["presentationState"]["choices"][number];

type Props = {
  choices: readonly Choice[];
  base: ActionPayloads["skit.advance"];
};

// 会話窓の上・右寄せで下から積み上げる固定寸法の板。ラベルは板の中央
// Fixed-size plates stacking upward from the window, right-aligned, with the label centered on each
export function SkitChoiceList({ choices, base }: Props) {
  const { t } = useI18n();

  return (
    <div className={styles.choices}
      onClick={(event) => event.stopPropagation()} onKeyDown={(event) => event.stopPropagation()}>
      {choices.map((choice) => (
        <button className={styles.choice} type="button" key={choice.choiceId}
          onClick={() => void dispatchAction("skit.select", { ...base, choiceId: choice.choiceId })}>
          <ChoiceMarkerIcon />
          <span className={styles.choiceLabel}>{choice.labelKey ? t(choice.labelKey) : choice.label}</span>
          <ChoiceMarkerIcon />
        </button>
      ))}
    </div>
  );
}
