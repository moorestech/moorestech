import { useState } from "react";
import { useTopic } from "../bridge/useTopic";
import { useItemMaster } from "../bridge/useItemMaster";
import { dispatchAction } from "../bridge/actions";
import type { CraftRecipe, CraftRecipesData, MachineRecipe, MachineRecipesData } from "../types/crafting";
import type { PlayerInventoryData } from "../types/inventory";
import type { ItemMasterEntry } from "../types/itemMaster";
import ItemSlot from "./ItemSlot";

type Props = {
  itemId: number | null;
  onSelect: (itemId: number) => void;
};

// 中央カラム: 選択アイテムのクラフトレシピと機械レシピを表示する（uGUI の RecipeViewer 相当）
// Center column: shows craft and machine recipes for the selected item, like uGUI's RecipeViewer
export default function RecipeViewer({ itemId, onSelect }: Props) {
  const recipes = useTopic<CraftRecipesData>("crafting.recipes");
  const machineRecipes = useTopic<MachineRecipesData>("crafting.machine_recipes");
  const inventory = useTopic<PlayerInventoryData>("local_player.inventory");
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
  // 対象アイテムを生産するクラフトレシピと、機械（blockItemId）ごとにまとめた機械レシピ
  // Craft recipes producing this item, plus machine recipes grouped by machine (blockItemId)
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
  const counts = new Map<number, number>();
  const addCount = (id: number, count: number) => {
    if (count > 0) counts.set(id, (counts.get(id) ?? 0) + count);
  };
  inventory.mainSlots.forEach((s) => addCount(s.itemId, s.count));
  inventory.hotbarSlots.forEach((s) => addCount(s.itemId, s.count));

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
              {t.blockItemId !== null ? (
                <img src={`/api/icons/${t.blockItemId}.png`} alt="" className="w-5 h-5 object-contain" draggable={false} />
              ) : null}
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

// 選択アイテムのアイコン+名前ヘッダ
// Icon + name header for the selected item
function ItemHeader({ itemId, name }: { itemId: number; name: string }) {
  return (
    <div className="flex items-center gap-2">
      <ItemSlot itemId={itemId} name={name} />
      <span className="text-base text-gray-200">{name}</span>
    </div>
  );
}

// 複数レシピの前後送りページャ（< i/n >）
// Pager for stepping through multiple recipes (< i/n >)
function RecipePager({
  index,
  count,
  setIndex,
}: {
  index: number;
  count: number;
  setIndex: (i: number) => void;
}) {
  if (count <= 1) return null;
  return (
    <div className="flex items-center gap-2 text-sm text-gray-300">
      <button onClick={() => setIndex((index + count - 1) % count)} className="bg-gray-700 hover:bg-gray-600 rounded px-2 py-0.5">
        &lt;
      </button>
      <span>
        {index + 1}/{count}
      </span>
      <button onClick={() => setIndex((index + 1) % count)} className="bg-gray-700 hover:bg-gray-600 rounded px-2 py-0.5">
        &gt;
      </button>
    </div>
  );
}

type CraftViewProps = {
  recipes: CraftRecipe[];
  recipeIndex: number;
  setRecipeIndex: (i: number) => void;
  counts: Map<number, number>;
  itemMaster: Map<number, ItemMasterEntry> | null;
  onSelect: (itemId: number) => void;
};

// クラフトタブ: 素材列 → 結果と Craft ボタン。素材クリックでそのアイテムへジャンプ
// Craft tab: material row → result with a Craft button; clicking a material jumps to that item
function CraftRecipeView({ recipes, recipeIndex, setRecipeIndex, counts, itemMaster, onSelect }: CraftViewProps) {
  // topic 更新でレシピ数が減った場合に備えて index をクランプ
  // Clamp the index in case a topic update shrank the recipe list
  const index = Math.min(recipeIndex, recipes.length - 1);
  const recipe = recipes[index];
  const craftable = recipe.requiredItems.every((r) => (counts.get(r.itemId) ?? 0) >= r.count);

  const onCraft = () => {
    if (!craftable) return;
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
          disabled={!craftable}
          className="ml-3 bg-blue-700 hover:bg-blue-600 disabled:bg-gray-700 disabled:text-gray-500 text-sm rounded px-4 py-2"
        >
          Craft
        </button>
      </div>
    </div>
  );
}

type MachineViewProps = {
  recipes: MachineRecipe[];
  recipeIndex: number;
  setRecipeIndex: (i: number) => void;
  itemMaster: Map<number, ItemMasterEntry> | null;
  onSelect: (itemId: number) => void;
};

// 機械タブ: 入力列 → 機械 → 出力列の閲覧表示（uGUI の MachineRecipeView 準拠、Craft ボタン無し）
// Machine tab: input row → machine → output row, view-only like uGUI's MachineRecipeView (no Craft button)
function MachineRecipeView({ recipes, recipeIndex, setRecipeIndex, itemMaster, onSelect }: MachineViewProps) {
  // topic 更新でレシピ数が減った場合に備えて index をクランプ
  // Clamp the index in case a topic update shrank the recipe list
  const index = Math.min(recipeIndex, recipes.length - 1);
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
