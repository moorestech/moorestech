import BlockIcon from "../BlockIcon";
import SlotFrame from "../SlotFrame";
import styles from "./style.module.css";

type Props = {
  blockId: number;
  name?: string;
  testId?: string;
};

// 機械レシピの従来どおり暗面を維持するブロックスロット
// Block slot that preserves the machine recipe's existing dark face
export default function BlockSlot({ blockId, name, testId }: Props) {
  // 白面化は後続較正へ送る
  // Defer the white face to uGUI comparison and omit data-filled here
  return (
    <SlotFrame testId={testId}>
      <BlockIcon blockId={blockId} alt={name} className={styles.icon} />
    </SlotFrame>
  );
}
