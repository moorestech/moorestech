import { ItemSlot } from "@/shared/ui";
import type { MachineRecipe, ItemMasterEntry } from "@/bridge/payloadTypes";
import { clampIndex } from "./craftLogic";
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
    <div className="space-y-2">
      <RecipePager index={index} count={recipes.length} setIndex={setRecipeIndex} />
      <div className="flex flex-wrap items-center gap-1">
        {recipe.inputItems.map((r, i) => (
          <ItemSlot key={i} itemId={r.itemId} count={r.count} name={itemMaster?.get(r.itemId)?.name} onLeftDown={() => onSelect(r.itemId)} />
        ))}
        <span className="mx-2 text-gray-400">→</span>
        <div className="flex flex-col items-center">
          <ItemSlot itemId={recipe.blockItemId} name={recipe.blockName} onLeftDown={() => onSelect(recipe.blockItemId)} />
          <span className="text-[10px] text-gray-400 max-w-16 truncate">{recipe.blockName}</span>
        </div>
        <span className="mx-2 text-gray-400">→</span>
        {recipe.outputItems.map((r, i) => (
          <ItemSlot key={i} itemId={r.itemId} count={r.count} name={itemMaster?.get(r.itemId)?.name} onLeftDown={() => onSelect(r.itemId)} />
        ))}
      </div>
    </div>
  );
}
