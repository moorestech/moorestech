using System.Collections.Generic;
using Core.Item;

namespace Game.Crafting.Interface
{
    public interface IIsCreatableJudgementService
    {
        public bool IsCreatable(IReadOnlyList<IItemStack> craftingItems);
        public IItemStack GetResult(IReadOnlyList<IItemStack> craftingItems);
        public CraftingConfigData GetCraftingConfigData(IReadOnlyList<IItemStack> craftingItems);

        public int CalcAllCraftItemNum(IReadOnlyList<IItemStack> craftingItems,
            IReadOnlyList<IItemStack> mainInventoryItems);

        public int CalcOneStackCraftItemNum(IReadOnlyList<IItemStack> craftingItems,
            IReadOnlyList<IItemStack> mainInventoryItems);
    }
}