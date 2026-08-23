import type { CSSProperties } from "react";
import type { ResolvedAnchor } from "@/shared/tutorialAnchor";
import styles from "./style.module.css";

type Props = { from: ResolvedAnchor | undefined; to: ResolvedAnchor | undefined };

// 2アンカーの中心を結ぶドラッグ誘導の矢印。両端が解決済みの時だけ描く
// The drag-guide arrow between two anchor centers, drawn only while both ends are resolved
export default function DragGuide({ from, to }: Props) {
  if (!from || from.status !== "ready" || !to || to.status !== "ready") return null;
  const fromX = from.rect.left + from.rect.width / 2;
  const fromY = from.rect.top + from.rect.height / 2;
  const toX = to.rect.left + to.rect.width / 2;
  const toY = to.rect.top + to.rect.height / 2;
  const dragGuideVars = { "--drag-guide-dx": `${toX - fromX}px`, "--drag-guide-dy": `${toY - fromY}px` } as CSSProperties;
  return <div className={styles.dragGuide} data-testid="tutorial-drag-guide"
    style={{ left: fromX, top: fromY, ...dragGuideVars }}>
    <svg viewBox="0 0 24 24" aria-hidden="true">
      <path d="M6 3 L18 12 L11 13.5 L13.5 20 L10.5 21 L8 14.5 L3 18 Z" />
    </svg>
  </div>;
}
