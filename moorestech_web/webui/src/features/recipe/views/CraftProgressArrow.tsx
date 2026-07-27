import { useId } from "react";
import { clamp01 } from "@/shared/clamp01";
import styles from "./CraftProgressArrow.module.css";

// 矢印グリフのpath。viewBox座標での水平範囲は x=2..119
// The arrow glyph path; its horizontal extent in viewBox units is x=2..119
const ARROW_PATH = "M2 27H69V2L119 39L69 76V51H2Z";
const ARROW_LEFT = 2;
const ARROW_SPAN = 117;
const ARROW_TOP = 0;
const ARROW_BOTTOM = 78;

// 矢印そのものが長押しクラフトの進捗ゲージ。溝の矢印へ充填色を左から重ねる（webui-design §8.12）
// The arrow itself is the hold-craft gauge: the fill tone is layered onto the track arrow from the left (webui-design §8.12)
export default function CraftProgressArrow({ value }: { value: number }) {
  const filled = clamp01(value);
  // 同一ページに矢印が並んでもclipが混線しないようidを一意化する（url(#…)を壊すコロンは除去）
  // Keep clip ids unique so several arrows never share one clip; colons that break url(#…) are stripped
  const instanceId = useId();
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
      {/* 溝→充填→輪郭の3層で同じ矢印を描き、充填だけを進捗幅の矩形で切り出す */}
      {/* Draw the same arrow as track, fill, and outline layers, clipping only the fill to the progress width */}
      <svg className={styles.craftArrowGlyph} viewBox="0 0 121 78" aria-hidden="true">
        <defs>
          <clipPath id={clipId}>
            <rect x={ARROW_LEFT} y={ARROW_TOP} width={ARROW_SPAN * filled} height={ARROW_BOTTOM} />
          </clipPath>
        </defs>
        <path className={styles.craftArrowTrack} d={ARROW_PATH} />
        <path className={styles.craftArrowFill} d={ARROW_PATH} clipPath={`url(#${clipId})`} />
        {/* 輪郭はclipを通さず最上層に置き、充填境界で矢印の形が途切れないようにする */}
        {/* The outline skips the clip and sits on top so the silhouette never breaks at the fill boundary */}
        <path className={styles.craftArrowOutline} d={ARROW_PATH} />
      </svg>
    </div>
  );
}
