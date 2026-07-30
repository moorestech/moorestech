import { Text } from "@mantine/core";
import styles from "./ItemHeader.module.css";

// 選択アイテムのハンマータブ+品名ヘッダ
// Hammer-tab + name header for the selected item
export default function ItemHeader({ name }: { name: string }) {
  return (
    <div className={styles.itemHeader}>
      {/* ハンマータブと主役の品名を縦にまとめる */}
      {/* Stack the hammer tab above the prominent item name */}
      <svg
        className={styles.toolTab}
        data-testid="craft-tab"
        viewBox="0 0 166 70"
        aria-hidden="true"
        focusable="false"
      >
        <path className={styles.toolTabBack} d="M15 0H125L166 70H0V10H15Z" />
        <path className={styles.toolTabSide} d="M125 0H143L166 70H145Z" />
        <path className={styles.toolTabFace} d="M24 10H115L135 70H24Z" />
        <path className={styles.toolTabEdge} d="M24 10H115L135 70H24Z" />
        <path className={styles.toolTabHammer} d="M46 66L79 33L75 29L82 22L87 27L90 24L99 33L96 36L101 41L94 48L88 42L85 45L82 42L55 70Z" />
      </svg>
      <Text className={styles.itemName}>{name}</Text>
      <div className={styles.itemHeaderRule} aria-hidden="true" />
    </div>
  );
}
