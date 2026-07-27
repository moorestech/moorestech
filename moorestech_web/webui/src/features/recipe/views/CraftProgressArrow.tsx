import { useId } from "react";
import { clamp01 } from "@/shared/clamp01";
import styles from "./CraftProgressArrow.module.css";

// 矢印pathと水平範囲・viewBox
// The arrow path, its horizontal extent, and viewBox size
const ARROW_PATH = "M2 27H69V2L119 39L69 76V51H2Z";
const ARROW_LEFT = 2;
const ARROW_RIGHT = 119;
const VIEWBOX_WIDTH = 121;
const VIEWBOX_HEIGHT = 78;
// clip矩形の幅はpath水平範囲から導き、path編集時のズレを防ぐ
// Derive the clip width from the path extent so editing the path cannot desync it
const ARROW_SPAN = ARROW_RIGHT - ARROW_LEFT;

// 矢印が長押しクラフト進捗ゲージ(§8.12)
// The arrow itself is the hold-craft progress gauge (§8.12)
export default function CraftProgressArrow({ value }: { value: number }) {
  const filled = clamp01(value);
  // 矢印が並んでもclipが混線しないようidを一意化する
  // Keep clip ids unique so several arrows never share one clip
  const instanceId = useId();
  // useIdのidは":r0:"形式で、コロンはurl(#…)参照を壊すため除去する
  // useId returns ":r0:"-style ids and the colons break url(#…) references, so strip them
  const clipId = `craft-arrow-fill-${instanceId.replace(/:/g, "")}`;

  return (
    <div
      className={styles.craftArrow}
      data-testid="craft-progress-arrow"
      role="progressbar"
      aria-valuemin={0}
      aria-valuemax={1}
      aria-valuenow={filled}
    >
      {/* 溝→充填→輪郭の3層。充填だけを進捗幅で切り出す */}
      {/* Track, fill, and outline layers; only the fill is clipped to the progress width */}
      <svg className={styles.craftArrowGlyph} viewBox={`0 0 ${VIEWBOX_WIDTH} ${VIEWBOX_HEIGHT}`} aria-hidden="true">
        <defs>
          <clipPath id={clipId}>
            <rect x={ARROW_LEFT} y={0} width={ARROW_SPAN * filled} height={VIEWBOX_HEIGHT} />
          </clipPath>
        </defs>
        <path className={styles.craftArrowTrack} d={ARROW_PATH} />
        <path className={styles.craftArrowFill} d={ARROW_PATH} clipPath={`url(#${clipId})`} />
        {/* 輪郭はclip外・最上層に置き、充填境界で形が途切れないようにする */}
        {/* The outline skips the clip and sits on top so the silhouette never breaks at the fill edge */}
        <path className={styles.craftArrowOutline} d={ARROW_PATH} />
      </svg>
    </div>
  );
}
