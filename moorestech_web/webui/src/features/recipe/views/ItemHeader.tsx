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
        <path className={styles.toolTabBack} d="M15 0H125L166 72H0V10H15Z" />
        <path className={styles.toolTabFace} d="M25 10H115L129 73H25Z" />
        <path className={styles.toolTabEdge} d="M25 10H115L129 73H25Z" />
        <path className={styles.toolTabSide} d="M117 9H126L142 73H134ZM15 9H24V73H15Z" />
        <path className={styles.toolTabHammer} d="M78 20H80V22H78ZM76 22H82V24H76ZM74 24H84V26H74ZM72 26H84V28H72ZM74 28H88V30H74ZM76 30H90V32H76ZM80 32H92V34H80ZM80 34H94V36H80ZM78 36H96V38H78ZM76 38H98V42H76ZM78 42H100V44H78ZM72 44H76V46H72ZM80 44H86V46H80ZM90 44H100V46H90ZM70 46H78V48H70ZM82 46H84V48H82ZM92 46H100V48H92ZM68 48H80V50H68ZM92 48H100V50H92ZM66 50H78V52H66ZM94 50H100V52H94ZM66 52H76V54H66ZM96 52H100V54H96ZM60 54H64V56H60ZM68 54H74V56H68ZM96 54H100V56H96ZM58 56H66V58H58ZM70 56H72V58H70ZM56 58H68V60H56ZM54 60H70V62H54ZM52 62H68V64H52ZM50 64H66V66H50ZM48 66H64V68H48ZM46 68H62V70H46ZM44 70H60V72H44Z" />
      </svg>
      <Text className={styles.itemName}>{name}</Text>
      <div className={styles.itemHeaderRule} aria-hidden="true" />
    </div>
  );
}
