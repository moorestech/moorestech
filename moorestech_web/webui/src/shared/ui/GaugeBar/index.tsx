// 0から1の値だけを描く、ドメイン非依存の読み取り専用水平ゲージ
// Domain-agnostic read-only horizontal gauge that renders only a zero-to-one value
import styles from "./style.module.css";

type Props = {
  value: number;
  testId?: string;
};

export default function GaugeBar({ value, testId }: Props) {
  const clampedValue = Math.min(1, Math.max(0, Number.isNaN(value) ? 0 : value));

  return (
    <div
      className={styles.track}
      data-testid={testId}
      role="progressbar"
      aria-valuemin={0}
      aria-valuemax={1}
      aria-valuenow={clampedValue}
    >
      <div className={styles.fill} style={{ width: `${clampedValue * 100}%` }} />
    </div>
  );
}
