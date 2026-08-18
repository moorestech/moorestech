import { useId } from "react";
import { clamp01 } from "@/shared/clamp01";
import styles from "./style.module.css";

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

// 寸法は`.arrow`がトークン直参照で単独保持する（§8.13: 呼び出し側に寸法ラッパーを作らない）
// `.arrow` alone owns the size via direct token references (§8.13: callers add no sizing wrapper)
// value=null は「進捗という概念が無い」表示で、progressbarロールも充填層も持たない静止矢印になる
// value=null means "no progress concept at all": a static arrow with neither the progressbar role nor a fill layer
export default function ProgressArrowGlyph({ value, testId }: { value: number | null; testId: string }) {
  const filled = value === null ? null : clamp01(value);
  const isGauge = filled !== null;
  // 矢印が並んでもclipが混線しないようidを一意化する
  // Keep clip ids unique so several arrows never share one clip
  const instanceId = useId();
  // useIdのidは":r0:"形式で、コロンはurl(#…)参照を壊すため除去する
  // useId returns ":r0:"-style ids and the colons break url(#…) references, so strip them
  const clipId = `progress-arrow-fill-${instanceId.replace(/:/g, "")}`;

  return (
    <div
      className={styles.arrow}
      data-testid={testId}
      role={isGauge ? "progressbar" : undefined}
      aria-valuemin={isGauge ? 0 : undefined}
      aria-valuemax={isGauge ? 1 : undefined}
      aria-valuenow={filled ?? undefined}
    >
      {/* 溝→充填→輪郭の3層。充填だけを進捗幅で切り出す（静止表示では充填層を持たない） */}
      {/* Track, fill, and outline layers; only the fill is clipped to the progress width (the static form has no fill) */}
      <svg className={styles.glyph} viewBox={`0 0 ${VIEWBOX_WIDTH} ${VIEWBOX_HEIGHT}`} aria-hidden="true">
        {isGauge && (
          <defs>
            <clipPath id={clipId}>
              <rect x={ARROW_LEFT} y={0} width={ARROW_SPAN * filled} height={VIEWBOX_HEIGHT} />
            </clipPath>
          </defs>
        )}
        <path className={styles.track} d={ARROW_PATH} />
        {isGauge && <path className={styles.fill} d={ARROW_PATH} clipPath={`url(#${clipId})`} />}
        {/* 輪郭はclip外・最上層に置き、充填境界で形が途切れないようにする */}
        {/* The outline skips the clip and sits on top so the silhouette never breaks at the fill edge */}
        <path className={styles.outline} d={ARROW_PATH} />
      </svg>
    </div>
  );
}
