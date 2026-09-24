// パネル内ビュー切替タブ。ModeSwitchと同面で選択中を表示
// In-panel view tabs; shows selection on the same face as ModeSwitch
import type { ReactNode } from "react";
import styles from "./style.module.css";

type PanelTabItem<T extends string> = { value: T; label: ReactNode; testId?: string };

type Props<T extends string> = {
  value: T;
  tabs: PanelTabItem<T>[];
  onChange: (value: T) => void;
  testId?: string;
};

export default function PanelTabs<T extends string>({ value, tabs, onChange, testId }: Props<T>) {
  return (
    <div className={styles.root} role="tablist" data-testid={testId}>
      {tabs.map((tab) => (
        <button
          key={tab.value}
          className={styles.tab}
          type="button"
          role="tab"
          aria-selected={tab.value === value}
          data-selected={tab.value === value ? "true" : undefined}
          data-testid={tab.testId}
          onClick={() => onChange(tab.value)}
        >
          {tab.label}
        </button>
      ))}
    </div>
  );
}
