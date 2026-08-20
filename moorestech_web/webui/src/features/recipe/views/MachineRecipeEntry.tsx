import { Box, Group, Stack, Text } from "@mantine/core";
import { ItemSlot, BlockIcon } from "@/shared/ui";
import type { MachineRecipe } from "@/bridge";
import RecipeRow from "./RecipeRow";
import styles from "./RecipeBox.module.css";
import { blockNameKey, L, useI18n } from "@/shared/i18n";

type Props = {
  recipe: MachineRecipe;
  onSelect: (itemId: number) => void;
  // testIdはレシピ単位で親が注入
  // Parent injects a per-recipe testId
  testId: string;
};

// 機械エントリ: 矢印はnullで静止
// ブロックアイコン+名前+秒数の情報行
// Machine entry: arrow stays static via null
// Info row: block icon + name + duration
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
