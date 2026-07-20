// 任意のラベルと値を扱う、ドメイン非依存の択一モード切替
// Domain-agnostic exclusive mode switch for arbitrary labels and values
import type { ReactNode } from "react";
import styles from "./style.module.css";

export type ModeSwitchOption = {
  value: string;
  label: ReactNode;
  testId?: string;
};

type Props = {
  value: string;
  options: ModeSwitchOption[];
  onChange: (value: string) => void;
  orientation?: "horizontal" | "vertical";
  testId?: string;
};

export default function ModeSwitch({ value, options, onChange, orientation = "horizontal", testId }: Props) {
  return (
    <div className={styles.root} data-orientation={orientation} data-testid={testId}>
      {options.map((option) => {
        const selected = option.value === value;
        return (
          <button
            className={styles.option}
            data-selected={selected ? "true" : undefined}
            data-testid={option.testId}
            aria-pressed={selected}
            key={option.value}
            type="button"
            onClick={() => onChange(option.value)}
          >
            {option.label}
          </button>
        );
      })}
    </div>
  );
}
