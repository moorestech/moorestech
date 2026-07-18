import { forwardRef, type ForwardedRef, type HTMLAttributes, type MouseEvent, type ReactNode } from "react";
import styles from "./style.module.css";

type Props = Omit<HTMLAttributes<HTMLDivElement>, "children" | "onMouseDown" | "onDoubleClick" | "onContextMenu"> & {
  children?: ReactNode;
  selected?: boolean;
  filled?: boolean;
  catalog?: boolean;
  onLeftDown?: (shiftKey: boolean) => void;
  onRightDown?: () => void;
  onDoubleClick?: () => void;
  testId?: string;
};

function createSlotFrameMouseDownHandler(onLeftDown: Props["onLeftDown"], onRightDown: Props["onRightDown"]) {
  return (event: MouseEvent<HTMLDivElement>) => {
    event.preventDefault();
    if (event.button === 0) onLeftDown?.(event.shiftKey);
    if (event.button === 2) onRightDown?.();
  };
}

// data属性と操作契約を集約する
// Centralizes the slot frame data attributes and pointer gesture contract
export function renderSlotFrame({ children, selected, filled, catalog, onLeftDown, onRightDown, onDoubleClick, testId, ...divProps }: Props, ref: ForwardedRef<HTMLDivElement>) {
  return (
    <div
      {...divProps}
      ref={ref}
      className={styles.slot}
      data-testid={testId}
      data-selected={selected ? "true" : undefined}
      data-filled={filled ? "true" : undefined}
      data-catalog={catalog ? "true" : undefined}
      onMouseDown={createSlotFrameMouseDownHandler(onLeftDown, onRightDown)}
      onDoubleClick={onDoubleClick}
      onContextMenu={(event) => event.preventDefault()}
    >
      {children}
    </div>
  );
}

const SlotFrame = forwardRef<HTMLDivElement, Props>(renderSlotFrame);
export default SlotFrame;
