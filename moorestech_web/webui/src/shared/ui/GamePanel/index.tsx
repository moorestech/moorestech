import type { CSSProperties, ReactNode } from "react";
import styles from "./style.module.css";

type Props = {
  gridArea?: string;
  title?: ReactNode;
  // craft: 中央詳細用の細め
  // craft: narrower variant for the center detail
  // skit: 画面下部の全幅会話帯
  // skit: full-bleed dialogue band at the screen bottom
  variant?: "default" | "craft" | "skit";
  style?: CSSProperties;
  children: ReactNode;
};

const VARIANT_CLASS_NAMES = { default: "", craft: styles.craft, skit: styles.skit };

// uGUI風の額縁パネル。タイトル+罫線+本文を囲う共通ラッパ
// uGUI-style framed panel wrapping title + deco rule + body
export default function GamePanel({ gridArea, title, variant = "default", style, children }: Props) {
  const variantClassName = VARIANT_CLASS_NAMES[variant];
  const className = variantClassName === "" ? styles.panel : `${styles.panel} ${variantClassName}`;
  return (
    <div className={className} style={{ gridArea, ...style }}>
      {title !== undefined ? (
        <>
          <div className={`${styles.decoLine} ${styles.decoLineTop}`} aria-hidden="true" />
          <div className={styles.header}>
            <h2 className={styles.title}>{title}</h2>
          </div>
          <div className={`${styles.decoLine} ${styles.decoLineBottom}`} aria-hidden="true" />
        </>
      ) : null}
      <div className={styles.body}>{children}</div>
      {/* default(持ち物/レシピ)パネルだけ下部に三角装飾3個を敷く。craftバリアントは対象外 */}
      {/* Only default (inventory/recipe) panels get the 3 bottom triangle decorations; the craft variant is excluded */}
      {variant === "default" ? (
        <div className={styles.bottomDeco} aria-hidden="true">
          <span />
          <span />
          <span />
        </div>
      ) : null}
    </div>
  );
}
