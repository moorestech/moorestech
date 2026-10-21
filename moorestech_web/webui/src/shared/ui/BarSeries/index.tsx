// 0から1の値を古い順に縦棒で並べる、ドメイン非依存の読み取り専用の時系列。nullの棒は溝だけを描く(まだ値が無い区間)
// Domain-agnostic read-only time series of zero-to-one values as vertical bars, oldest first; a null bar draws only its track (no value yet)
import styles from "./style.module.css";

type Props = {
  values: ReadonlyArray<number | null>;
  testId?: string;
};

export default function BarSeries({ values, testId }: Props) {
  return (
    <div className={styles.series} role="img" data-testid={testId}>
      {values.map((value, index) => {
        const clampedValue = value === null ? 0 : Math.min(1, Math.max(0, Number.isNaN(value) ? 0 : value));
        return (
          <div key={index} className={styles.bar} data-empty={value === null ? "true" : undefined}>
            <div className={styles.fill} style={{ height: `${clampedValue * 100}%` }} />
          </div>
        );
      })}
    </div>
  );
}
