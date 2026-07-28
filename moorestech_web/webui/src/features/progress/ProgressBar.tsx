import { Text } from "@mantine/core";
import { useTopic, Topics } from "@/bridge";
import { tutorialAnchor, TutorialAnchorIds } from "@/shared/tutorialAnchor";
import { GaugeBar } from "@/shared/ui";
import styles from "./style.module.css";

// uGUI ProgressBarView を模した表示専用オーバーレイ。visible で Show/Hide を切り替える。
// Display-only overlay mirroring uGUI ProgressBarView; visible toggles Show/Hide.
export function ProgressBar() {
  const data = useTopic(Topics.progress);

  // 初回スナップショット前(null)や非表示時は何も描画しない。
  // Render nothing before the first snapshot (null) or while hidden.
  if (!data || !data.visible) return null;

  // 単一ゲージにラベルと進捗表示
  // Render the label and progress in one gauge
  return (
    <div
      data-testid="progress-bar"
      className={styles.wrapper}
      {...tutorialAnchor(TutorialAnchorIds.miningHud)}
    >
      {data.label != null && <Text className={styles.label}>{data.label}</Text>}
      <GaugeBar value={data.progress} testId="progress-gauge" />
    </div>
  );
}
