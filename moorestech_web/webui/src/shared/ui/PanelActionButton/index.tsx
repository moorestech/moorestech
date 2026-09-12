// パネル付随の副次アクション用押しボタン。ドメイン語彙を持たず、面と寸法だけを様式として供給する
// Secondary action button attached to a panel; it supplies only the face and dimensions, never domain vocabulary
import type { ReactNode } from "react";
import styles from "./style.module.css";

type Props = {
  onClick: () => void;
  children: ReactNode;
  // 押せない状態の表現。ModeSwitch と同じく data-disabled を公開する
  // Expresses the unpressable state, exposing data-disabled like ModeSwitch does
  disabled?: boolean;
  testId?: string;
};

export default function PanelActionButton({ onClick, children, disabled, testId }: Props) {
  return (
    <button
      className={styles.button}
      type="button"
      data-disabled={disabled || undefined}
      data-testid={testId}
      disabled={disabled}
      onClick={onClick}
    >
      {children}
    </button>
  );
}
