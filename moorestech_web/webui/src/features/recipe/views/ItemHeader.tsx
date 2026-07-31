import { Text } from "@mantine/core";
import styles from "./ItemHeader.module.css";
import { L, useI18n } from "@/shared/i18n";

// 選択アイテムのハンマータブ+品名ヘッダ
// Hammer-tab + name header for the selected item
export default function ItemHeader({ name }: { name: string }) {
  const { t } = useI18n();
  return (
    <div className={styles.itemHeader}>
      {/* ハンマータブと主役の品名を縦にまとめる */}
      {/* Stack the hammer tab above the prominent item name */}
      <div className={styles.toolTab} aria-hidden="true">{t(L.ui.recipe.hammerIcon)}</div>
      <Text className={styles.itemName}>{name}</Text>
      <div className={styles.itemHeaderRule} aria-hidden="true" />
    </div>
  );
}
