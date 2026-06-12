import { useState } from "react";
import { useTopic } from "../bridge/useTopic";
import { useItemMaster } from "../bridge/useItemMaster";
import { dispatchAction } from "../bridge/actions";
import type { CraftRecipe, CraftRecipesData } from "../types/crafting";
import type { PlayerInventoryData } from "../types/inventory";
import ItemSlot from "./ItemSlot";

// アンロック済みレシピの一覧表示とクラフト実行
// Lists unlocked recipes and executes crafts
export default function CraftPanel() {
  const recipes = useTopic<CraftRecipesData>("crafting.recipes");
  const inventory = useTopic<PlayerInventoryData>("local_player.inventory");
  const itemMaster = useItemMaster();
  const [selectedGuid, setSelectedGuid] = useState<string | null>(null);

  if (!recipes || !inventory) {
    return <div className="text-sm text-gray-400">connecting...</div>;
  }

  // サーバーの OneClickCraft は main+hotbar のみ参照するため、grab は所持数に含めない
  // The server's OneClickCraft only consults main+hotbar, so grab is excluded from the tally
  const counts = new Map<number, number>();
  const addCount = (itemId: number, count: number) => {
    if (count > 0) counts.set(itemId, (counts.get(itemId) ?? 0) + count);
  };
  inventory.mainSlots.forEach((s) => addCount(s.itemId, s.count));
  inventory.hotbarSlots.forEach((s) => addCount(s.itemId, s.count));

  const isCraftable = (recipe: CraftRecipe) =>
    recipe.requiredItems.every((r) => (counts.get(r.itemId) ?? 0) >= r.count);

  const selected = recipes.recipes.find((r) => r.recipeGuid === selectedGuid) ?? null;

  const onCraft = () => {
    if (!selected || !isCraftable(selected)) return;
    void dispatchAction("craft.execute", { recipeGuid: selected.recipeGuid });
  };

  return (
    <div className="space-y-3">
      <h2 className="text-lg font-semibold">Craft</h2>
      <div className="grid grid-cols-9 gap-1 w-fit">
        {recipes.recipes.map((r) => (
          <div key={r.recipeGuid} className={isCraftable(r) ? "" : "opacity-40"}>
            <ItemSlot
              itemId={r.resultItemId}
              count={r.resultCount}
              name={itemMaster?.get(r.resultItemId)?.name}
              selected={r.recipeGuid === selectedGuid}
              onLeftDown={() => setSelectedGuid(r.recipeGuid)}
            />
          </div>
        ))}
      </div>
      {selected ? (
        <div className="space-y-2">
          <div className="text-sm text-gray-300">
            {itemMaster?.get(selected.resultItemId)?.name ?? `item ${selected.resultItemId}`} ×{selected.resultCount}
          </div>
          <div className="flex gap-1 items-center">
            {selected.requiredItems.map((r, i) => (
              <div key={i} className={(counts.get(r.itemId) ?? 0) >= r.count ? "" : "opacity-40"}>
                <ItemSlot itemId={r.itemId} count={r.count} name={itemMaster?.get(r.itemId)?.name} />
              </div>
            ))}
            <button
              onClick={onCraft}
              disabled={!isCraftable(selected)}
              className="ml-3 bg-blue-700 hover:bg-blue-600 disabled:bg-gray-700 disabled:text-gray-500 text-sm rounded px-4 py-2"
            >
              Craft
            </button>
          </div>
        </div>
      ) : null}
    </div>
  );
}
