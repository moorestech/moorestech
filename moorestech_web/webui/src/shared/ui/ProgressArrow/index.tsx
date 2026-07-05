import { clamp01 } from "@/shared/clamp01";
import styles from "./style.module.css";

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
