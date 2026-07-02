import styles from "./style.module.css";

// 進捗値を 0..1 に丸める（uGUI slider.value と同じ範囲）。NaN は 0 扱い
// Clamp the progress value to 0..1 (same range as uGUI slider.value); NaN treated as 0
function clamp01(n: number): number {
  if (Number.isNaN(n)) return 0;
  if (n < 0) return 0;
  if (n > 1) return 1;
  return n;
}

// 0..1 を幅 % で満たす横向き進捗矢印。uGUI ProgressArrowView 相当
// Horizontal progress arrow filling by width %; mirrors uGUI ProgressArrowView
export default function ProgressArrow({ value }: { value: number }) {
  const percent = `${clamp01(value) * 100}%`;
  return (
    <div data-testid="progress-arrow" className={styles.track}>
      <div className={styles.fill} style={{ width: percent }} />
    </div>
  );
}
