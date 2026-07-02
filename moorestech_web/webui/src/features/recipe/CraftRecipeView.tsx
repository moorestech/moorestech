import { Box, Button, Group, Stack, Text } from "@mantine/core";
import { dispatchAction } from "@/bridge";
import { ItemSlot } from "@/shared/ui";
import type { CraftRecipe, ItemMasterEntry } from "@/bridge/payloadTypes";
import { clampIndex, craftable } from "./craftLogic";
import RecipePager from "./RecipePager";

type Props = {
  recipes: CraftRecipe[];
  recipeIndex: number;
  setRecipeIndex: (i: number) => void;
  counts: Map<number, number>;
  itemMaster: Map<number, ItemMasterEntry> | null;
  onSelect: (itemId: number) => void;
};

// クラフトタブ: 素材列 → 結果と Craft ボタン。素材クリックでそのアイテムへジャンプ
// Craft tab: material row → result with a Craft button; clicking a material jumps to that item
export default function CraftRecipeView({ recipes, recipeIndex, setRecipeIndex, counts, itemMaster, onSelect }: Props) {
  // topic 更新でレシピ数が減った場合に備えて index をクランプ
  // Clamp the index in case a topic update shrank the recipe list
  const index = clampIndex(recipeIndex, recipes.length);
  const recipe = recipes[index];
  const isCraftable = craftable(recipe, counts);

  const onCraft = () => {
    if (!isCraftable) return;
    void dispatchAction("craft.execute", { recipeGuid: recipe.recipeGuid });
  };

  return (
    <Stack gap="xs">
      <RecipePager index={index} count={recipes.length} setIndex={setRecipeIndex} />
      <Group gap={4} align="center" wrap="wrap">
        {recipe.requiredItems.map((r, i) => (
          // 所持数不足の素材は 40% 透過で強調を落とす（uGUI 準拠）
          // Dim insufficient materials to 40% opacity, matching uGUI
          <Box key={i} opacity={(counts.get(r.itemId) ?? 0) >= r.count ? 1 : 0.4}>
            <ItemSlot itemId={r.itemId} count={r.count} name={itemMaster?.get(r.itemId)?.name} onLeftDown={() => onSelect(r.itemId)} />
          </Box>
        ))}
        <Text c="dimmed" mx="xs">→</Text>
        <ItemSlot itemId={recipe.resultItemId} count={recipe.resultCount} name={itemMaster?.get(recipe.resultItemId)?.name} />
        <Button color="blue" size="sm" ml="sm" disabled={!isCraftable} onClick={onCraft}>
          Craft
        </Button>
      </Group>
    </Stack>
  );
}
