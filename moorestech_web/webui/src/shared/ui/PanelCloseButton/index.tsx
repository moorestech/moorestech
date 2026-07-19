// パネル右上へ浮かせて使う、面を持たない共通閉じるボタン
// Shared faceless close button that floats at a panel's top-right corner
import type { ButtonHTMLAttributes } from "react";
import styles from "./style.module.css";

type DataAttributes = {
  [key: `data-${string}`]: string | number | boolean | null | undefined;
};

type Props = Omit<ButtonHTMLAttributes<HTMLButtonElement>, "aria-label" | "children" | "onClick"> & DataAttributes & {
  onClick: () => void;
  ariaLabel: string;
  testId?: string;
};

export default function PanelCloseButton({ onClick, ariaLabel, testId, className, ...rest }: Props) {
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
      <svg className={styles.icon} viewBox="0 0 16 16" aria-hidden="true" focusable="false">
        <path d="M3 3L13 13M13 3L3 13" />
      </svg>
    </button>
  );
}
