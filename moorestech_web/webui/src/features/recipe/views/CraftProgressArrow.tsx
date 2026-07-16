import { clamp01 } from "@/shared/clamp01";
import styles from "../RecipeViewer.module.css";

// 素材→結果を示す白い矢印。長押し進捗は下の細いバーで控えめに示す（uGUI の白矢印に合わせる）
// White arrow from materials to result; hold progress shows subtly in the thin bar below (matches uGUI's white arrow)
export default function CraftProgressArrow({ value }: { value: number }) {
  return (
    <div className={styles.craftArrow}>
      <span className={styles.craftArrowGlyph} aria-hidden="true">➔</span>
      <div className={styles.craftArrowTrack}>
        <div className={styles.craftArrowFill} style={{ width: `${clamp01(value) * 100}%` }} />
      </div>
    </div>
  );
}
