import { Group, Stack, Text } from "@mantine/core";
import { ItemSlot, BlockIcon } from "@/shared/ui";
import slotStyles from "@/shared/ui/ItemSlot/style.module.css";
import type { MachineRecipe, ItemMasterEntry } from "@/bridge";
import { clampIndex } from "../craftLogic";
import RecipePager from "./RecipePager";

type Props = {
  recipes: MachineRecipe[];
  recipeIndex: number;
  setRecipeIndex: (i: number) => void;
  itemMaster: Map<number, ItemMasterEntry> | null;
  onSelect: (itemId: number) => void;
};

// 機械タブ: 入力列 → 機械 → 出力列の閲覧表示（uGUI の MachineRecipeView 準拠、Craft ボタン無し）
// Machine tab: input row → machine → output row, view-only like uGUI's MachineRecipeView (no Craft button)
export default function MachineRecipeView({ recipes, recipeIndex, setRecipeIndex, itemMaster, onSelect }: Props) {
  // topic 更新でレシピ数が減った場合に備えて index をクランプ
  // Clamp the index in case a topic update shrank the recipe list
  const index = clampIndex(recipeIndex, recipes.length);
  const recipe = recipes[index];

  return (
    <Stack gap="xs">
      <RecipePager index={index} count={recipes.length} setIndex={setRecipeIndex} />
      <Group gap={4} align="center" wrap="wrap">
        {recipe.inputItems.map((r, i) => (
          <ItemSlot key={i} itemId={r.itemId} count={r.count} name={itemMaster?.get(r.itemId)?.name} onLeftDown={() => onSelect(r.itemId)} />
        ))}
        <Text c="dimmed" mx="xs">→</Text>
        <Stack gap={0} align="center">
          <div className={slotStyles.slot}>
            <BlockIcon blockId={recipe.blockId} alt={recipe.blockName} className={slotStyles.icon} />
          </div>
          <Text fz={10} c="dimmed" maw="4rem" truncate="end">{recipe.blockName}</Text>
        </Stack>
        <Text c="dimmed" mx="xs">→</Text>
        {recipe.outputItems.map((r, i) => (
          <ItemSlot key={i} itemId={r.itemId} count={r.count} name={itemMaster?.get(r.itemId)?.name} onLeftDown={() => onSelect(r.itemId)} />
        ))}
      </Group>
    </Stack>
  );
}
