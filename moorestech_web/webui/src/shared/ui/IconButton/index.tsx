// パネル隅やHUDへ浮かせて使う、面を持たない共通アイコンボタン
// Shared faceless icon button that floats at a panel corner or on the HUD
import type { ButtonHTMLAttributes, ReactNode } from "react";
import styles from "./style.module.css";

type DataAttributes = {
  [key: `data-${string}`]: string | number | boolean | null | undefined;
};

type Props = Omit<ButtonHTMLAttributes<HTMLButtonElement>, "aria-label" | "children" | "onClick"> & DataAttributes & {
  onClick: () => void;
  ariaLabel: string;
  testId?: string;
  // 省略時は既定の×。閉じる以外の用途は呼び出し側がインラインSVGを渡す
  // Defaults to the cross; callers pass an inline SVG for any other use
  children?: ReactNode;
};

export default function IconButton({ onClick, ariaLabel, testId, className, children, ...rest }: Props) {
  const buttonClassName = className === undefined ? styles.button : `${styles.button} ${className}`;

  return (
    <button
      className={buttonClassName}
      type="button"
      aria-label={ariaLabel}
      data-testid={testId}
      onClick={onClick}
      {...rest}
    >
      {children ?? <CloseIcon />}
    </button>
  );
}

// ×は画像を使わず、親ボタンの文字色を継承する線だけで表す
// The cross uses no image and consists only of lines inheriting the button color
function CloseIcon() {
  return (
    <svg className={styles.icon} viewBox="0 0 16 16" aria-hidden="true" focusable="false">
      <path d="M3 3L13 13M13 3L3 13" />
    </svg>
  );
}
