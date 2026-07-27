// スキットのアイコンはすべてインラインSVG。UI装飾の画像アセット化はデザイン哲学§6で禁止
// Every skit icon is inline SVG; shipping image assets for UI decoration is forbidden by design philosophy §6
import styles from "./style.module.css";

// 送り待ちの二重山形シェブロン（uGUI nav_arrow.png 由来）。光彩・点滅は付けない
// Double chevron marking "waiting to advance" (from uGUI nav_arrow.png); no glow, no blinking
export function AdvanceMarkerIcon() {
  return (
    <svg className={styles.advanceMarker} viewBox="0 0 24 16" aria-hidden="true" focusable="false">
      <path d="M4 2 L12 8 L20 2" />
      <path d="M4 8 L12 14 L20 8" />
    </svg>
  );
}

// 選択肢の板の両端に載る菱形マーカー（uGUI btn__select_*.png 由来）
// Diamond marker riding each end of a choice plate (from uGUI btn__select_*.png)
export function ChoiceMarkerIcon() {
  return (
    <svg viewBox="0 0 12 12" aria-hidden="true" focusable="false">
      <path d="M6 0.5 L11.5 6 L6 11.5 L0.5 6 Z" />
      <path d="M6 3.5 L8.5 6 L6 8.5 L3.5 6 Z" />
    </svg>
  );
}

// 自動送り。円の中の再生三角で「放っておいても進む」を表す
// Auto-advance; a play triangle inside a circle reads as "progresses on its own"
export function AutoIcon() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true" focusable="false">
      <circle cx="12" cy="12" r="9" />
      <path d="M10 8 L16 12 L10 16 Z" />
    </svg>
  );
}

// スキップ。二連シェブロン+終端バー
// Skip; a double chevron with a trailing bar
export function SkipIcon() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true" focusable="false">
      <path d="M4 6 L10 12 L4 18" />
      <path d="M11 6 L17 12 L11 18" />
      <path d="M20 6 L20 18" />
    </svg>
  );
}

// UI非表示・復帰は同じ目のアイコンで、非表示側だけ斜線を足す
// Hide/show UI share one eye icon; only the hide side adds a slash
export function HideUiIcon() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true" focusable="false">
      <path d="M2.5 12 C6 6.5 9 5 12 5 C15 5 18 6.5 21.5 12 C18 17.5 15 19 12 19 C9 19 6 17.5 2.5 12 Z" />
      <circle cx="12" cy="12" r="3" />
      <path d="M4 4 L20 20" />
    </svg>
  );
}

export function ShowUiIcon() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true" focusable="false">
      <path d="M2.5 12 C6 6.5 9 5 12 5 C15 5 18 6.5 21.5 12 C18 17.5 15 19 12 19 C9 19 6 17.5 2.5 12 Z" />
      <circle cx="12" cy="12" r="3" />
    </svg>
  );
}
