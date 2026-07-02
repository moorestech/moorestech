import type { ReactNode, WheelEvent } from "react";
import styles from "./style.module.css";

type Props = {
  children: ReactNode;
  // 省略時は uGUI 標準の 9 列
  // Defaults to the uGUI-standard 9 columns when omitted
  cols?: number;
  testId?: string;
  onWheel?: (e: WheelEvent<HTMLDivElement>) => void;
  className?: string;
};

// スロットを固定幅セルで並べる共通グリッド（inventory/hotbar/block/itemList で共用）
// Shared fixed-cell slot grid used by inventory, hotbar, block, and item list views
export default function SlotGrid({ children, cols, testId, onWheel, className }: Props) {
  return (
    <div
      data-testid={testId}
      className={className ? `${styles.grid} ${className}` : styles.grid}
      style={{ gridTemplateColumns: `repeat(${cols ?? 9}, max-content)` }}
      onWheel={onWheel}
    >
      {children}
    </div>
  );
}
