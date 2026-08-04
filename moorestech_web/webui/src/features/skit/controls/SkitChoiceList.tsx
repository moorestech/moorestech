import { dispatchAction, type ActionPayloads, type SkitPresentationData } from "@/bridge";
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
  return (
    <div className={styles.choices}>
      {choices.map((choice) => (
        <button className={styles.choice} type="button" key={choice.choiceId}
          onClick={() => void dispatchAction("skit.select", { ...base, choiceId: choice.choiceId })}>
          <ChoiceMarkerIcon className={`${styles.marker} ${styles.markerStart}`} />
          {/* Unity側resolverで解決済みの表示文字列をpushするためt()を通さない */}
          {/* Unity pushes resolver-completed display strings, so they bypass t() */}
          <span className={styles.choiceLabel}>{choice.label}</span>
          <ChoiceMarkerIcon className={`${styles.marker} ${styles.markerEnd}`} />
        </button>
      ))}
    </div>
  );
}
