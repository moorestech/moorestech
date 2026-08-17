import { Box, Group, Stack, Text } from "@mantine/core";
import { ItemSlot, BlockIcon, ProgressArrowGlyph } from "@/shared/ui";
import type { MachineRecipe } from "@/bridge";
import styles from "./RecipeBox.module.css";
import craftArrowStyles from "./craftArrow.module.css";
import { blockNameKey, L, useI18n } from "@/shared/i18n";

type Props = {
  recipe: MachineRecipe;
  onSelect: (itemId: number) => void;
};

// 機械エントリ: クラフトエントリと同じレシピ行ベース（矢印はvalue=0の静止表示）+
// ブロックアイコン/名前/秒数のクリック不可情報行（ボタン相当、ADR 0011）
// Machine entry: same recipe row base as craft (arrow shown static at value=0) plus a
// non-interactive block icon/name/duration info row in place of the button (ADR 0011)
export default function MachineRecipeEntry({ recipe, onSelect }: Props) {
  const { t } = useI18n();
  const localizedBlockName = t(blockNameKey(recipe.blockGuid));

  return (
    <Stack className={styles.recipeEntry} gap="xs" data-testid="machine-recipe-entry">
      <div className={styles.recipeBox}>
        <Group gap={0} className={styles.recipeMaterials}>
          {recipe.inputItems.map((r, i) => (
            <Box className={styles.materialSlot} key={i}>
              {/* 機械レシピは手クラフトしないため必要数のみ表示する（所持数チェックなし） */}
              {/* Machine recipes are not hand-crafted, so show required counts only (no owned-count check) */}
              <ItemSlot itemId={r.itemId} count={r.count} onLeftDown={() => onSelect(r.itemId)} />
            </Box>
          ))}
        </Group>
        <Box className={styles.recipeArrowCol}>
          <div className={craftArrowStyles.craftArrow}>
            <ProgressArrowGlyph value={0} testId="machine-progress-arrow" />
          </div>
        </Box>
        <Box className={styles.recipeResult}>
          {recipe.outputItems.map((r, i) => (
            <ItemSlot key={i} itemId={r.itemId} count={r.count} onLeftDown={() => onSelect(r.itemId)} />
          ))}
        </Box>
      </div>
      <Group className={styles.machineInfoRow} gap="xs" justify="center" wrap="nowrap">
        <BlockIcon blockId={recipe.blockId} className={styles.machineInfoIcon} />
        <Text className={styles.machineInfoText} truncate="end">{localizedBlockName}</Text>
        <Text className={styles.machineInfoText}>{t(L.ui.recipe.duration, { seconds: recipe.time })}</Text>
      </Group>
    </Stack>
  );
}
