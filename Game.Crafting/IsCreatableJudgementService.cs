using System.Collections.Generic;
using Core.Item;
using Game.Crafting.Interface;

namespace Game.Crafting
{
    public class IsCreatableJudgementService : IIsCreatableJudgementService
    {
        private readonly ICraftingConfig _craftingConfig;
        private readonly Dictionary<string, CraftingConfigData> _craftingConfigDataCache = new();

        public IsCreatableJudgementService(ICraftingConfig craftingConfig)
        {
            //TODO コンストラクタ_craftingConfigDataCacheの作成
            _craftingConfig = craftingConfig;
        }

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