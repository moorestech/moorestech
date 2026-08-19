import { clamp01 } from "@/shared/clamp01";
import styles from "./style.module.css";

// 0..1 を幅 % で満たす帯状の進捗ゲージ。矢印グリフゲージ(ProgressArrowGlyph)とは器が違う
// Bar-shaped progress gauge filled by width %; a different vessel from the arrow-glyph gauge (ProgressArrowGlyph)
export default function ProgressArrowBar({ value }: { value: number }) {
  const percent = `${clamp01(value) * 100}%`;
  return (
    <div data-testid="progress-arrow-bar" className={styles.track}>
      <div className={styles.fill} style={{ width: percent }} />
    </div>
  );
}
