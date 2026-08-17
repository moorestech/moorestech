import { Box, Group, Stack, Text } from "@mantine/core";
import { ItemSlot, BlockIcon } from "@/shared/ui";
import type { MachineRecipe } from "@/bridge";
import RecipeRow from "./RecipeRow";
import styles from "./RecipeBox.module.css";
import { blockNameKey, L, useI18n } from "@/shared/i18n";

type Props = {
  recipe: MachineRecipe;
  onSelect: (itemId: number) => void;
  // 同一アイテムを出す機械レシピが複数並ぶため、レシピ単位で一意なtestIdを親が注入する
  // Several machine recipes can yield one item, so the parent injects a per-recipe unique testId
  testId: string;
};

// 機械エントリ: クラフトエントリと同じレシピ行ベース（矢印は進捗概念が無いためnullで静止）+
// ブロックアイコン/名前/秒数のクリック不可情報行（ボタン相当、ADR 0011）
// Machine entry: same recipe row base as craft (arrow static via null since there is no progress) plus a
// non-interactive block icon/name/duration info row in place of the button (ADR 0011)
export default function MachineRecipeEntry({ recipe, onSelect, testId }: Props) {
  const { t } = useI18n();
  const localizedBlockName = t(blockNameKey(recipe.blockGuid));

  return (
    <Stack className={styles.recipeEntry} gap="xs" data-testid={testId}>
      <RecipeRow
        testId={`machine-recipe-box-${recipe.recipeGuid}`}
        arrowValue={null}
        arrowTestId={`machine-progress-arrow-${recipe.recipeGuid}`}
        materials={recipe.inputItems.map((r, i) => (
          <Box className={styles.materialSlot} key={i}>
            {/* 機械レシピは手クラフトしないため必要数のみ表示する（所持数チェックなし） */}
            {/* Machine recipes are not hand-crafted, so show required counts only (no owned-count check) */}
            <ItemSlot itemId={r.itemId} count={r.count} onLeftDown={() => onSelect(r.itemId)} />
          </Box>
        ))}
        result={recipe.outputItems.map((r, i) => (
          <ItemSlot key={i} itemId={r.itemId} count={r.count} onLeftDown={() => onSelect(r.itemId)} />
        ))}
      />
      <Group className={styles.machineInfoRow} gap="xs" justify="center" wrap="nowrap">
        <BlockIcon blockId={recipe.blockId} alt={localizedBlockName} className={styles.machineInfoIcon} />
        <Text className={styles.machineInfoText} truncate="end">{localizedBlockName}</Text>
        <Text className={styles.machineInfoText}>{t(L.ui.recipe.duration, { seconds: recipe.time })}</Text>
      </Group>
    </Stack>
  );
}
