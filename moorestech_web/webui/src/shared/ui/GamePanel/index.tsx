import type { CSSProperties, ReactNode } from "react";
import styles from "./style.module.css";

type Props = {
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

// uGUI風の額縁パネル。タイトル+罫線+本文を囲う共通ラッパ
// uGUI-style framed panel wrapping title + deco rule + body
export default function GamePanel({ gridArea, title, headerRight, variant = "default", style, testId, children }: Props) {
  const className = variant === "craft" ? `${styles.panel} ${styles.craft}` : styles.panel;
  return (
    <div className={className} style={{ gridArea, ...style }} data-testid={testId}>
      {title !== undefined ? (
        <>
          <div className={styles.header}>
            <h2 className={styles.title}>{title}</h2>
            {headerRight ? <span className={styles.headerRight}>{headerRight}</span> : null}
          </div>
          <div className={styles.decoLine} />
        </>
      ) : null}
      <div className={styles.body}>{children}</div>
    </div>
  );
}
