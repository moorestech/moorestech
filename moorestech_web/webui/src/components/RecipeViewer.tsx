import { useState } from "react";
import { useTopic } from "../bridge/useTopic";
import { useItemMaster } from "../bridge/useItemMaster";
import type { CraftRecipesData, MachineRecipe, MachineRecipesData } from "../types/crafting";
import type { PlayerInventoryData } from "../types/inventory";
import type { ItemMasterEntry } from "../types/itemMaster";
import { buildOwnedCounts } from "../features/recipe/craftLogic";
import ItemHeader from "./ItemHeader";
import ItemIcon from "./ItemIcon";
import CraftRecipeView from "./CraftRecipeView";
import MachineRecipeView from "./MachineRecipeView";

type Props = {
  itemId: number | null;
  onSelect: (itemId: number) => void;
};

// 中央カラム: 選択アイテムのクラフトレシピと機械レシピを表示する（uGUI の RecipeViewer 相当）
// Center column: shows craft and machine recipes for the selected item, like uGUI's RecipeViewer
export default function RecipeViewer({ itemId, onSelect }: Props) {
  const recipes = useTopic("crafting.recipes");
  const machineRecipes = useTopic("crafting.machine_recipes");
  const inventory = useTopic("local_player.inventory");
  const itemMaster = useItemMaster();

  const loaded = recipes !== null && machineRecipes !== null && inventory !== null;

  return (
    <div className="space-y-3 [grid-area:viewer] min-w-0">
      <h2 className="text-lg font-semibold">Recipe</h2>
      {!loaded ? (
        <div className="text-sm text-gray-400">connecting...</div>
      ) : itemId === null ? (
        <div className="text-sm text-gray-400">右のアイテムリストからアイテムを選択してください</div>
      ) : (
        <RecipeContent
          key={itemId}
          itemId={itemId}
          recipes={recipes}
          machineRecipes={machineRecipes}
          inventory={inventory}
          itemMaster={itemMaster}
          onSelect={onSelect}
        />
      )}
    </div>
  );
}

type ContentProps = {
  itemId: number;
  recipes: CraftRecipesData;
  machineRecipes: MachineRecipesData;
  inventory: PlayerInventoryData;
  itemMaster: Map<number, ItemMasterEntry> | null;
  onSelect: (itemId: number) => void;
};

// タブ定義。blockItemId が null ならクラフトレシピのタブ
// Tab descriptor; blockItemId null means the craft recipe tab
type Tab = { key: string; label: string; blockItemId: number | null };

// 選択アイテムのレシピ本体。key={itemId} で再マウントされタブ・ページ状態がリセットされる
// Recipe body for the selected item; remounted via key={itemId} so tab/page state resets
function RecipeContent({ itemId, recipes, machineRecipes, inventory, itemMaster, onSelect }: ContentProps) {
  // 生産レシピを抽出し機械別に集約
  // Collect producing recipes, grouped per machine
  const craftRecipes = recipes.recipes.filter((r) => r.resultItemId === itemId);
  const machineGroups = new Map<number, MachineRecipe[]>();
  machineRecipes.recipes
    .filter((r) => r.outputItems.some((o) => o.itemId === itemId))
    .forEach((r) => {
      const group = machineGroups.get(r.blockItemId) ?? [];
      group.push(r);
      machineGroups.set(r.blockItemId, group);
    });

  // タブ一覧: クラフトタブ + 機械ごとに1タブ（uGUI の RecipeTabView 相当）
  // Tab list: a craft tab plus one tab per machine, like uGUI's RecipeTabView
  const tabs: Tab[] = [];
  if (craftRecipes.length > 0) tabs.push({ key: "craft", label: "クラフト", blockItemId: null });
  machineGroups.forEach((group, blockItemId) => tabs.push({ key: `m${blockItemId}`, label: group[0].blockName, blockItemId }));

  const [tabKey, setTabKey] = useState(tabs[0]?.key ?? "");
  const [recipeIndex, setRecipeIndex] = useState(0);
  // topic 更新でタブ構成が変わった場合は先頭タブへフォールバック
  // Fall back to the first tab if a topic update changed the tab set
  const activeTab = tabs.find((t) => t.key === tabKey) ?? tabs[0] ?? null;

  const itemName = itemMaster?.get(itemId)?.name ?? `item ${itemId}`;

  if (activeTab === null) {
    return (
      <div className="space-y-3">
        <ItemHeader itemId={itemId} name={itemName} />
        <div className="text-sm text-gray-400">このアイテムのレシピはありません</div>
      </div>
    );
  }

  // サーバーの OneClickCraft は main+hotbar のみ参照するため、grab は所持数に含めない
  // The server's OneClickCraft only consults main+hotbar, so grab is excluded from the tally
  const counts = buildOwnedCounts(inventory);

  return (
    <div className="space-y-3">
      <ItemHeader itemId={itemId} name={itemName} />
      {tabs.length > 1 ? (
        <div className="flex flex-wrap gap-1">
          {tabs.map((t) => (
            <button
              key={t.key}
              onClick={() => {
                setTabKey(t.key);
                setRecipeIndex(0);
              }}
              className={`flex items-center gap-1 text-sm rounded px-2 py-1 ${
                t.key === activeTab.key ? "bg-gray-600 text-white" : "bg-gray-800 text-gray-300 hover:bg-gray-700"
              }`}
            >
              {t.blockItemId !== null ? <ItemIcon itemId={t.blockItemId} className="w-5 h-5 object-contain" /> : null}
              {t.label}
            </button>
          ))}
        </div>
      ) : null}
      {activeTab.blockItemId === null ? (
        <CraftRecipeView
          recipes={craftRecipes}
          recipeIndex={recipeIndex}
          setRecipeIndex={setRecipeIndex}
          counts={counts}
          itemMaster={itemMaster}
          onSelect={onSelect}
        />
      ) : (
        <MachineRecipeView
          recipes={machineGroups.get(activeTab.blockItemId)!}
          recipeIndex={recipeIndex}
          setRecipeIndex={setRecipeIndex}
          itemMaster={itemMaster}
          onSelect={onSelect}
        />
      )}
    </div>
  );
}
