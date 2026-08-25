import HoverTooltip from "../HoverTooltip";
import BlockIcon from "../BlockIcon";
import SlotFrame from "../SlotFrame";
import styles from "./style.module.css";

type Props = {
  blockId: number;
  name?: string;
  testId?: string;
};

// 暗面維持ブロックスロット。ホバー名はTooltip表示
// Block slot preserving the dark face; hover name via Tooltip
export default function BlockSlot({ blockId, name, testId }: Props) {
  // 白面化は後続較正へ送る
  // Defer the white face to uGUI comparison and omit data-filled here
  return (
    <HoverTooltip label={name} disabled={!name}>
      <SlotFrame testId={testId}>
        <BlockIcon blockId={blockId} alt={name} className={styles.icon} />
      </SlotFrame>
    </HoverTooltip>
  );
}
