import { Button, Text } from "@mantine/core";
import styles from "../RecipeViewer.module.css";

// 選択アイテムのハンマータブ+品名ヘッダ
// Hammer-tab + name header for the selected item
export default function ItemHeader({ name }: { name: string }) {
  return (
    <div className={styles.itemHeader}>
      {/* ハンマータブと主役の品名を縦にまとめる */}
      {/* Stack the hammer tab above the prominent item name */}
      <div className={styles.toolTab} aria-hidden="true">🔨</div>
      <Text className={styles.itemName}>{name}</Text>
      {/* 中央のダガーと多層ダイヤを見出し境界へ配置する */}
      {/* Place the central daggers and layered diamond on the header boundary */}
      <div className={styles.itemHeaderRule} data-testid="recipe-divider-ornament" aria-hidden="true">
        <span className={styles.dividerDiamond} />
      </div>
      {/* レシピツリー連携前の見た目確認用プレースホルダ */}
      {/* Visual placeholder until recipe-tree integration is implemented */}
      <Button className={styles.recipeTreeButton} size="compact-sm" onClick={() => {}}>
        レシピツリーで表示
      </Button>
    </div>
  );
}
