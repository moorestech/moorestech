import { clamp01 } from "@/shared/clamp01";
import styles from "./CraftProgressArrow.module.css";

// 素材→結果を示す白い矢印。長押し進捗は下の細いバーで控えめに示す（uGUI の白矢印に合わせる）
// White arrow from materials to result; hold progress shows subtly in the thin bar below (matches uGUI's white arrow)
export default function CraftProgressArrow({ value }: { value: number }) {
  return (
    <div className={styles.craftArrow}>
      {/* 正本の直線的な矢印輪郭をSVGで固定する */}
      {/* Fix the reference's angular arrow silhouette with SVG */}
      <svg className={styles.craftArrowGlyph} viewBox="0 0 121 78" aria-hidden="true">
        <path d="M2 27H69V2L119 39L69 76V51H2Z" />
      </svg>
      <div className={styles.craftArrowTrack}>
        <div className={styles.craftArrowFill} style={{ width: `${clamp01(value) * 100}%` }} />
      </div>
    </div>
  );
}
