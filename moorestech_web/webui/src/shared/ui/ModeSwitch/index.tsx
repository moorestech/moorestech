// 任意のラベルと値を扱う、ドメイン非依存の択一モード切替
// Domain-agnostic exclusive mode switch for arbitrary labels and values
import type { ReactNode } from "react";
import styles from "./style.module.css";

export type ModeSwitchOption<T extends string = string> = {
  value: T;
  label: ReactNode;
  testId?: string;
  // root全体無効とは別物
  // Distinct from the root-level whole-switch disabled
  disabled?: boolean;
};

type Props<T extends string> = {
  // 無選択はnull。空文字センチネルを呼び出し側へ広げない
  // No selection is null; this keeps an empty-string sentinel from spreading to callers
  value: T | null;
  options: ModeSwitchOption<T>[];
  onChange: (value: T) => void;
  orientation?: "horizontal" | "vertical";
  // パネル内ビュー切替（§8.22）はtablist、既定の択一モードはgroup
  // In-panel view switching (§8.22) is a tablist; the default exclusive mode is a group
  role?: "group" | "tablist";
  disabled?: boolean;
  testId?: string;
};

export default function ModeSwitch<T extends string>({ value, options, onChange, orientation = "horizontal", role = "group", disabled, testId }: Props<T>) {
  const tablist = role === "tablist";
  return (
    <div
      className={styles.root}
      role={tablist ? "tablist" : undefined}
      data-orientation={orientation}
      data-disabled={disabled || undefined}
      data-testid={testId}
    >
      {options.map((option) => {
        const selected = option.value === value;
        const optionDisabled = disabled || option.disabled === true;
        return (
          <button
            className={styles.option}
            data-selected={selected ? "true" : undefined}
            data-option-disabled={option.disabled ? "true" : undefined}
            data-testid={option.testId}
            role={tablist ? "tab" : undefined}
            aria-selected={tablist ? selected : undefined}
            aria-pressed={tablist ? undefined : selected}
            key={option.value}
            type="button"
            disabled={optionDisabled}
            onClick={() => onChange(option.value)}
          >
            {option.label}
          </button>
        );
      })}
    </div>
  );
}
