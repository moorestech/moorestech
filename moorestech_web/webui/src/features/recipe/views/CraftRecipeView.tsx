import { useEffect } from "react";
import { Box, Button, Group, Stack } from "@mantine/core";
import { dispatchAction } from "@/bridge";
import { ItemSlot, ProgressArrow } from "@/shared/ui";
import type { CraftRecipe, ItemMasterEntry } from "@/bridge/contract/payloadTypes";
import { clampIndex, craftable } from "../craftLogic";
import { useHoldCraft } from "../useHoldCraft";
import RecipePager from "./RecipePager";

type Props = {
  recipes: CraftRecipe[];
  recipeIndex: number;
  setRecipeIndex: (i: number) => void;
  counts: Map<number, number>;
  itemMaster: Map<number, ItemMasterEntry> | null;
  onSelect: (itemId: number) => void;
};

// クラフトタブ: 素材列 → 進捗矢印 → 結果。ボタン長押しで craftTime ごとに連続クラフト（uGUI CraftButton 準拠）
// Craft tab: material row → progress arrow → result; hold the button to continuously craft every craftTime (mirrors uGUI CraftButton)
export default function CraftRecipeView({ recipes, recipeIndex, setRecipeIndex, counts, itemMaster, onSelect }: Props) {
  // topic 更新でレシピ数が減った場合に備えて index をクランプ
  // Clamp the index in case a topic update shrank the recipe list
  const index = clampIndex(recipeIndex, recipes.length);
  const recipe = recipes[index];
  const isCraftable = craftable(recipe, counts);

  // 長押し1周ごとに1回クラフト要求を送る。素材チェックはサーバー側で行われる
  // Send one craft request per completed hold cycle; material checks happen server-side
  const { progress, isHolding, start, stop } = useHoldCraft(recipe.craftTime, isCraftable, () => {
    void dispatchAction("craft.execute", { recipeGuid: recipe.recipeGuid });
  });

  // 表示レシピが切り替わったら進行中の長押しを打ち切る
  // Abort any in-progress hold when the displayed recipe changes
  useEffect(() => stop, [recipe.recipeGuid, stop]);

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
        <Box mx="xs">
          <ProgressArrow value={isHolding ? progress : 0} />
        </Box>
        <ItemSlot itemId={recipe.resultItemId} count={recipe.resultCount} name={itemMaster?.get(recipe.resultItemId)?.name} />
        <Button
          color="blue"
          size="sm"
          ml="sm"
          disabled={!isCraftable}
          title="長押しでクラフト（押し続けで連続クラフト）"
          onPointerDown={(e) => {
            // ボタン外へドラッグしても pointerup を受け取れるよう捕捉する
            // Capture the pointer so pointerup fires even if dragged off the button
            e.currentTarget.setPointerCapture(e.pointerId);
            start();
          }}
          onPointerUp={stop}
          onPointerCancel={stop}
        >
          Craft
        </Button>
      </Group>
    </Stack>
  );
}
