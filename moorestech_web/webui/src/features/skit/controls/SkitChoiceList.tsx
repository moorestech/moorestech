import { dispatchAction, type ActionPayloads, type SkitPresentationData } from "@/bridge";
import { useI18n } from "@/shared/i18n";
import { ChoiceMarkerIcon } from "../icons";
import styles from "./SkitChoiceList.module.css";

type Choice = SkitPresentationData["presentationState"]["choices"][number];

type Props = {
  choices: readonly Choice[];
  base: ActionPayloads["skit.advance"];
};

// 会話窓上・右寄せの固定寸法の板。ラベルは中央
// Fixed-size plates above the window, right-aligned, label centered
export function SkitChoiceList({ choices, base }: Props) {
  const { t } = useI18n();

  return (
    <div className={styles.choices}>
      {choices.map((choice) => (
        <button className={styles.choice} type="button" key={choice.choiceId}
          onClick={() => void dispatchAction("skit.select", { ...base, choiceId: choice.choiceId })}>
          <ChoiceMarkerIcon className={`${styles.marker} ${styles.markerStart}`} />
          {/* labelKey無しの生labelはUnity所有の表示データのためt()を通さない */}
          {/* A raw label without labelKey is Unity-owned display data and bypasses t() */}
          <span className={styles.choiceLabel}>{choice.labelKey ? t(choice.labelKey) : choice.label}</span>
          <ChoiceMarkerIcon className={`${styles.marker} ${styles.markerEnd}`} />
        </button>
      ))}
    </div>
  );
}
