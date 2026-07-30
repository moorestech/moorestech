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
          {/* 選択肢はPlan 2でC#側が表示直前に解決し、Webは解決済みlabelだけを描画する */}
          {/* Plan 2 resolves choices in C# immediately before display; Web renders only the resolved label */}
          <span className={styles.choiceLabel}>{choice.label}</span>
          <ChoiceMarkerIcon className={`${styles.marker} ${styles.markerEnd}`} />
        </button>
      ))}
    </div>
  );
}
