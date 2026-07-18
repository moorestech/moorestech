import { Group, Stack, Text } from "@mantine/core";
import { ItemSlot, BlockSlot } from "@/shared/ui";
import type { MachineRecipe } from "@/bridge";
import { clampIndex } from "@/shared/clampIndex";
import RecipePager from "./RecipePager";
import { useI18n } from "@/shared/i18n";

type Props = {
  recipes: MachineRecipe[];
  recipeIndex: number;
  setRecipeIndex: (i: number) => void;
  onSelect: (itemId: number) => void;
};

// 機械タブ: 入力列 → 機械 → 出力列の閲覧表示（uGUI の MachineRecipeView 準拠、Craft ボタン無し）
// Machine tab: input row → machine → output row, view-only like uGUI's MachineRecipeView (no Craft button)
export default function MachineRecipeView({ recipes, recipeIndex, setRecipeIndex, onSelect }: Props) {
  const { t } = useI18n();
  // topic 更新でレシピ数が減った場合に備えて index をクランプ
  // Clamp the index in case a topic update shrank the recipe list
  const index = clampIndex(recipeIndex, recipes.length);
  const recipe = recipes[index];

  return (
    <Stack gap="xs">
      <RecipePager index={index} count={recipes.length} setIndex={setRecipeIndex} />
      <Group gap={4} align="center" wrap="wrap">
        {recipe.inputItems.map((r, i) => (
          <ItemSlot key={i} itemId={r.itemId} count={r.count} onLeftDown={() => onSelect(r.itemId)} />
        ))}
        <Text c="dimmed" mx="xs">{t("→")}</Text>
        <Stack gap={0} align="center">
          <BlockSlot blockId={recipe.blockId} name={recipe.blockName} />
          <Text fz={10} c="dimmed" maw="4rem" truncate="end">{recipe.blockName}</Text>
        </Stack>
        <Text c="dimmed" mx="xs">{t("→")}</Text>
        {recipe.outputItems.map((r, i) => (
          <ItemSlot key={i} itemId={r.itemId} count={r.count} onLeftDown={() => onSelect(r.itemId)} />
        ))}
      </Group>
    </Stack>
  );
}
