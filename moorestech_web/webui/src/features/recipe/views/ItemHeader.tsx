import { Group, Text } from "@mantine/core";
import { ItemSlot } from "@/shared/ui";
import styles from "../RecipeViewer.module.css";

// 選択アイテムのアイコン+名前ヘッダ
// Icon + name header for the selected item
export default function ItemHeader({ itemId, name }: { itemId: number; name: string }) {
  return (
    <div className={styles.itemHeader}>
      {/* 道具タブと選択アイテムをカード中央へまとめる */}
      {/* Center the tool tab and selected item within the card */}
      <span className={styles.toolTab} aria-hidden="true">🔨</span>
      <Group gap="xs" justify="center">
        <ItemSlot itemId={itemId} name={name} />
        <Text>{name}</Text>
      </Group>
      <div className={styles.itemHeaderRule} aria-hidden="true" />
    </div>
  );
}
