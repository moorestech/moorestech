import type { CSSProperties, ReactNode } from "react";
import styles from "./style.module.css";
import hudVariantStyles from "./hudVariant.module.css";

// craft: 中央詳細用の細め
// craft: narrower variant for the center detail
// skit: 画面下部の全幅会話帯
// skit: full-bleed dialogue band at the screen bottom
// hud: 面と境界フェードのみ持つ
// hud: face and boundary fade only, for resident HUDs
export type GamePanelVariant = "default" | "craft" | "skit" | "hud";

type Props = {
  gridArea?: string;
  title?: ReactNode;
  variant?: GamePanelVariant;
  style?: CSSProperties;
  children: ReactNode;
};

// Record注釈でvariant追加時のクラス割当漏れをマップ定義側の型エラーにする
// The Record annotation turns a missing class mapping for a new variant into a type error here
const VARIANT_CLASS_NAMES: Record<GamePanelVariant, string> = {
  default: "", craft: styles.craft, skit: styles.skit, hud: hudVariantStyles.hud,
};

// uGUI風の額縁パネル。タイトル+罫線+本文を囲う共通ラッパ
// uGUI-style framed panel wrapping title + deco rule + body
export default function GamePanel({ gridArea, title, variant = "default", style, children }: Props) {
  const variantClassName = VARIANT_CLASS_NAMES[variant];
  const className = variantClassName === "" ? styles.panel : `${styles.panel} ${variantClassName}`;
  return (
    <div className={className} data-variant={variant} style={{ gridArea, ...style }}>
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
