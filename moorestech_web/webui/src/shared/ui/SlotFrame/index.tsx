import { forwardRef, type ForwardedRef, type HTMLAttributes, type ReactNode } from "react";
import { useSlotMouse } from "../useSlotMouse";
import styles from "./style.module.css";

type Props = Omit<HTMLAttributes<HTMLDivElement>, "children" | "onMouseDown" | "onDoubleClick" | "onContextMenu"> & {
  children?: ReactNode;
  selected?: boolean;
  filled?: boolean;
  catalog?: boolean;
  insufficient?: boolean;
  onLeftDown?: (shiftKey: boolean) => void;
  onRightDown?: () => void;
  onRightEnter?: () => void;
  onLeftEnter?: () => void;
  onDoubleClick?: () => void;
  testId?: string;
};

// data属性と操作契約を集約する
// Centralizes the slot frame data attributes and pointer gesture contract
export function renderSlotFrame({ children, selected, filled, catalog, insufficient, onLeftDown, onRightDown, onRightEnter, onLeftEnter, onDoubleClick, testId, ...divProps }: Props, ref: ForwardedRef<HTMLDivElement>) {
  const slotMouse = useSlotMouse(onLeftDown, onRightDown, onRightEnter, onLeftEnter);
  return (
    <div
      {...divProps}
      ref={ref}
      className={styles.slot}
      data-testid={testId}
      data-selected={selected ? "true" : undefined}
      data-filled={filled ? "true" : undefined}
      data-catalog={catalog ? "true" : undefined}
      data-insufficient={insufficient ? "true" : undefined}
      onMouseDown={slotMouse.onMouseDown}
      onMouseEnter={slotMouse.onMouseEnter}
      onDoubleClick={onDoubleClick}
      onContextMenu={slotMouse.onContextMenu}
    >
      {children}
    </div>
  );
}

const SlotFrame = forwardRef<HTMLDivElement, Props>(renderSlotFrame);
export default SlotFrame;
