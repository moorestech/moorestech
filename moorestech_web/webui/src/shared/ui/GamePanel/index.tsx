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

type SharedProps = {
  style?: CSSProperties;
  children: ReactNode;
};

// 額縁を持つvariantだけがグリッド配置とタイトル行（右端の副次アクション付き）を受け取る
// Only framed variants accept grid placement and a title row with its trailing secondary action
type FramedProps = SharedProps & {
  variant?: "default" | "craft";
  gridArea?: string;
  title?: ReactNode;
  titleAction?: ReactNode;
};

// 面だけのvariantはタイトル行を持たないため、型でtitle系を到達不能にする
// Face-only variants have no title row, so the type makes title-side props unreachable
type FaceOnlyProps = SharedProps & {
  variant: "skit" | "hud";
};

type Props = FramedProps | FaceOnlyProps;

// variant追加漏れを型エラー化する
// Turns a missing variant mapping into a type error
const VARIANT_CLASS_NAMES: Record<GamePanelVariant, string> = {
  default: "", craft: styles.craft, skit: styles.skit, hud: hudVariantStyles.hud,
};

// uGUI風の額縁パネル。タイトル+罫線+本文を囲う共通ラッパ
// uGUI-style framed panel wrapping title + deco rule + body
export default function GamePanel(props: Props) {
  const framed = props.variant === undefined || props.variant === "default" || props.variant === "craft" ? props : null;
  const variant = props.variant ?? "default";
  const variantClassName = VARIANT_CLASS_NAMES[variant];
  const className = variantClassName ? `${styles.panel} ${variantClassName}` : styles.panel;
  return (
    <div className={className} data-variant={variant} style={{ gridArea: framed?.gridArea, ...props.style }}>
      {framed !== null && framed.title !== undefined ? (
        <>
          <div className={`${styles.decoLine} ${styles.decoLineTop}`} aria-hidden="true" />
          <div className={styles.header}>
            <h2 className={styles.title}>{framed.title}</h2>
            {/* 副次アクションはタイトル行右端へ絶対配置し、タイトルの実測オフセットへ干渉させない */}
            {/* The secondary action is absolutely placed at the title row's right end so it never disturbs the title's measured offsets */}
            {framed.titleAction !== undefined ? <div className={styles.titleAction}>{framed.titleAction}</div> : null}
          </div>
          <div className={`${styles.decoLine} ${styles.decoLineBottom}`} aria-hidden="true" />
        </>
      ) : null}
      <div className={styles.body}>{props.children}</div>
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
