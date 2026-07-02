import { Progress, Text } from "@mantine/core";
import { useTopic, Topics } from "@/bridge";
import { percentValue } from "./progressLogic";
import styles from "./style.module.css";

// uGUI ProgressBarView を模した表示専用オーバーレイ。visible で Show/Hide を切り替える。
// Display-only overlay mirroring uGUI ProgressBarView; visible toggles Show/Hide.
export function ProgressBar() {
  const data = useTopic(Topics.progress);

  // 初回スナップショット前(null)や非表示時は何も描画しない。
  // Render nothing before the first snapshot (null) or while hidden.
  if (!data || !data.visible) return null;

  // 画面下部中央に固定し、任意ラベル・トラック・割合フィルを重ねる。
  // Pin to the bottom-center, layering the optional label, track, and proportional fill.
  return (
    <div data-testid="progress-bar" className={styles.wrapper}>
      {data.label != null && (
        <Text size="sm" c="dimmed" mb={4}>{data.label}</Text>
      )}
      <Progress.Root size="md" transitionDuration={0}>
        <Progress.Section data-testid="progress-fill" value={percentValue(data.progress)} color="green" />
      </Progress.Root>
    </div>
  );
}
