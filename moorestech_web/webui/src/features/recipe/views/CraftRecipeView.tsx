import { useEffect } from "react";
import { Box, Button, Group, Stack, Text } from "@mantine/core";
import { dispatchAction } from "@/bridge";
import { ItemSlot } from "@/shared/ui";
import type { CraftRecipe, ItemMasterEntry } from "@/bridge/contract/payloadTypes";
import { clampIndex, craftable } from "../craftLogic";
import { useHoldCraft } from "../useHoldCraft";
import styles from "../RecipeViewer.module.css";
import RecipePager from "./RecipePager";
import CraftProgressArrow from "./CraftProgressArrow";

type Props = {
  recipes: CraftRecipe[];
  recipeIndex: number;
  setRecipeIndex: (i: number) => void;
  counts: Map<number, number>;
  itemMaster: Map<number, ItemMasterEntry> | null;
  onSelect: (itemId: number) => void;
};

// クラフトタブ: 素材列 → 進捗矢印 → 結果。下端ボタン長押しで craftTime ごとに連続クラフト（uGUI CraftButton 準拠）
// Craft tab: material row → progress arrow → result; hold the bottom button to continuously craft every craftTime (mirrors uGUI CraftButton)
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
    <Stack className={styles.craftRecipe} gap="xs">
      <RecipePager index={index} count={recipes.length} setIndex={setRecipeIndex} />
      <Group className={styles.recipeBox} data-testid="craft-recipe-box" gap={4} align="center" wrap="wrap">
        {recipe.requiredItems.map((r, i) => (
          // 所持数不足の素材は 40% 透過で強調を落とす（uGUI 準拠）
          // Dim insufficient materials to 40% opacity, matching uGUI
          <Box key={i} opacity={(counts.get(r.itemId) ?? 0) >= r.count ? 1 : 0.4}>
            <ItemSlot itemId={r.itemId} count={r.count} name={itemMaster?.get(r.itemId)?.name} onLeftDown={() => onSelect(r.itemId)} />
          </Box>
        ))}
        {/* 素材と完成品の間に長押し進捗を矢印で表示する */}
        {/* Show hold progress as an arrow between materials and result */}
        <Box mx="xs">
          <CraftProgressArrow value={isHolding ? progress : 0} />
        </Box>
        <ItemSlot itemId={recipe.resultItemId} count={recipe.resultCount} name={itemMaster?.get(recipe.resultItemId)?.name} />
        {/* 製作時間を選択枠の下端中央に含める */}
        {/* Keep the craft duration inside the lower edge of the selection frame */}
        <Text className={styles.craftTime} size="sm">{recipe.craftTime}秒</Text>
      </Group>
      <Button
        className={styles.craftButton}
        fullWidth
        disabled={!isCraftable}
        title="長押しでクラフト（押し続けで連続クラフト）"
        // 主ボタン（左クリック/主タッチ）以外では長押しを開始しない
        // Only the primary button/touch starts the hold; ignore right/middle clicks
        onPointerDown={(e) => { if (e.button === 0) start(); }}
        // 離す・ボタンから外れる・キャンセルのいずれでもクラフトを止め、経過時間をリセットする
        // Release, leaving the button, or cancel all stop the craft and reset the elapsed time
        onPointerUp={stop}
        onPointerLeave={stop}
        onPointerCancel={stop}
        // キーボード（Enter/Space）長押しでも連続クラフトできるようにする（ネイティブ onClick 喪失分の回復）
        // Keep keyboard (Enter/Space) hold working, restoring the craft path lost when native onClick was removed
        onKeyDown={(e) => { if (e.key === "Enter" || e.key === " ") { e.preventDefault(); start(); } }}
        onKeyUp={(e) => { if (e.key === "Enter" || e.key === " ") stop(); }}
        onBlur={stop}
      >
        Craft
      </Button>
    </Stack>
  );
}
