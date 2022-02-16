using System.Collections.Generic;
using Core.Item;
using Game.Crafting.Interface;

namespace Game.Crafting
{
    public class IsCreatableJudgementService : IIsCreatableJudgementService
    {
        public bool IsCreatable(List<IItemStack> craftingItems)
        {
            throw new System.NotImplementedException();
        }

        public IItemStack GetResult(List<IItemStack> craftingItems)
        {
            throw new System.NotImplementedException();
        }

        public CraftingConfigData GetCraftingConfigData(List<IItemStack> craftingItems)
        {
            throw new System.NotImplementedException();
        }
    }
}