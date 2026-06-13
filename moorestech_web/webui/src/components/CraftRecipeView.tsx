import { dispatchAction } from "../bridge/actions";
import { clampIndex, craftable } from "../features/recipe/craftLogic";
import type { CraftRecipe } from "../types/crafting";
import type { ItemMasterEntry } from "../types/itemMaster";
import ItemSlot from "./ItemSlot";
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
    <div className="space-y-2">
      <RecipePager index={index} count={recipes.length} setIndex={setRecipeIndex} />
      <div className="flex flex-wrap items-center gap-1">
        {recipe.requiredItems.map((r, i) => (
          <div key={i} className={(counts.get(r.itemId) ?? 0) >= r.count ? "" : "opacity-40"}>
            <ItemSlot itemId={r.itemId} count={r.count} name={itemMaster?.get(r.itemId)?.name} onLeftDown={() => onSelect(r.itemId)} />
          </div>
        ))}
        <span className="mx-2 text-gray-400">→</span>
        <ItemSlot itemId={recipe.resultItemId} count={recipe.resultCount} name={itemMaster?.get(recipe.resultItemId)?.name} />
        <button
          onClick={onCraft}
          disabled={!isCraftable}
          className="ml-3 bg-blue-700 hover:bg-blue-600 disabled:bg-gray-700 disabled:text-gray-500 text-sm rounded px-4 py-2"
        >
          Craft
        </button>
      </div>
    </div>
  );
}
