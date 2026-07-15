import type { CSSProperties, ReactNode } from "react";
import styles from "./style.module.css";

type Props = {
  // grid-template-areas 上の配置先
  // Placement target on the grid-template-areas
  gridArea?: string;
  title?: ReactNode;
  headerRight?: ReactNode;
  // craft は中央詳細用の細めバリアント
  // "craft" is the narrower variant for the center detail
  variant?: "default" | "craft";
  style?: CSSProperties;
  testId?: string;
  children: ReactNode;
};

// uGUI 風の額縁パネル。タイトル＋装飾罫線＋本文を紺の枠で囲う共通ラッパ
// uGUI-style framed panel: a shared wrapper enclosing title + deco rule + body in a navy frame
export default function GamePanel({ gridArea, title, headerRight, variant = "default", style, testId, children }: Props) {
  const className = variant === "craft" ? `${styles.panel} ${styles.craft}` : styles.panel;
  return (
    <div className={className} style={{ gridArea, ...style }} data-testid={testId}>
      {title !== undefined ? (
        <>
          <div className={styles.header}>
            <span className={styles.title}>{title}</span>
            {headerRight ? <span className={styles.headerRight}>{headerRight}</span> : null}
          </div>
          <div className={styles.decoLine} />
        </>
      ) : null}
      <div className={styles.body}>{children}</div>
    </div>
  );
}
