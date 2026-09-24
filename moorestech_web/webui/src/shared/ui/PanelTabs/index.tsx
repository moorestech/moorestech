// パネル内のビュー切替タブ。ModeSwitchと同じ面で選択中を示す
// In-panel view tabs use ModeSwitch faces and expose the selected tab
import type { ReactNode } from "react";
import styles from "./style.module.css";

export type PanelTab = { value: string; label: ReactNode; testId?: string };

type Props = {
  value: string;
  tabs: PanelTab[];
  onChange: (value: string) => void;
  testId?: string;
};

export default function PanelTabs({ value, tabs, onChange, testId }: Props) {
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
