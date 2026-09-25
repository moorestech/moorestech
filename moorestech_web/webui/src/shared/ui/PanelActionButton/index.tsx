// パネル付随の副次アクション用押しボタン。ドメイン語彙を持たず、面と寸法だけを様式として供給する
// Secondary action button attached to a panel; it supplies only the face and dimensions, never domain vocabulary
import type { ButtonHTMLAttributes, ReactNode } from "react";
import styles from "./style.module.css";

type DataAttributes = {
  [key: `data-${string}`]: string | number | boolean | null | undefined;
};

type Props = Omit<ButtonHTMLAttributes<HTMLButtonElement>, "children" | "onClick"> & DataAttributes & {
  onClick: () => void;
  children: ReactNode;
  testId?: string;
};

export default function PanelActionButton({ onClick, children, testId, className, ...rest }: Props) {
  const buttonClassName = className === undefined ? styles.button : `${styles.button} ${className}`;
  return (
    <button className={buttonClassName} type="button" data-testid={testId} onClick={onClick} {...rest}>
      {children}
    </button>
  );
}
