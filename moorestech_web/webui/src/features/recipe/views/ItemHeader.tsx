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
        <path className={styles.toolTabSide} d="M118 8H125L143 72H130ZM15 9H28V72H15Z" />
        <path className={styles.toolTabFace} d="M25 10H115L129 73H25Z" />
        <path className={styles.toolTabEdge} d="M24 10H115L135 72H24Z" />
        <path className={styles.toolTabHammer} d="M42 66L46 66L79 33L75 29L82 18L87 27L90 24L99 33L96 36L100 41L94 48L88 42L85 45L82 42L55 74Z" />
      </svg>
      <Text className={styles.itemName}>{name}</Text>
      <div className={styles.itemHeaderRule} aria-hidden="true" />
    </div>
  );
}
