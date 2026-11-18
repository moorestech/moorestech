using System;
using System.Collections.Generic;
using Client.Ui.State.Items;
using Core.Master;

namespace Client.Ui.State.Recipe.Data
{
    /// <summary>
    /// 解放済みクラフトレシピ 1 件の値（Web bridge の CraftRecipe の写し）
    /// One unlocked craft recipe (port of the Web bridge CraftRecipe)
    /// </summary>
    public sealed class CraftRecipeData
    {
        public Guid RecipeGuid { get; }
        public ItemId ResultItemId { get; }
        public int ResultCount { get; }
        public double CraftTime { get; }
        public IReadOnlyList<ItemAmount> RequiredItems { get; }

        public CraftRecipeData(Guid recipeGuid, ItemId resultItemId, int resultCount, double craftTime, IReadOnlyList<ItemAmount> requiredItems)
        {
            RecipeGuid = recipeGuid;
            ResultItemId = resultItemId;
            ResultCount = resultCount;
            CraftTime = craftTime;
            RequiredItems = requiredItems;
        }
    }
}
