import { Text } from "@mantine/core";
import styles from "./ItemHeader.module.css";

// 選択アイテムの品名ヘッダ（装飾タブはADR 0011で廃止）
// Item name header; the decorative tab was removed by ADR 0011
export default function ItemHeader({ name }: { name: string }) {
  return (
    <div className={styles.itemHeader}>
      <Text className={styles.itemName}>{name}</Text>
      <div className={styles.itemHeaderRule} aria-hidden="true" />
    </div>
  );
}
