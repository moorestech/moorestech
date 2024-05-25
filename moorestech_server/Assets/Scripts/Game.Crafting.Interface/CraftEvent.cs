using System;
using UniRx;

namespace Game.Crafting.Interface
{
    public class CraftEvent
    {
        public IObserver<CraftingConfigData> OnCraftItem => _onCraftItem;
        private readonly Subject<CraftingConfigData> _onCraftItem = new();
        
        public void InvokeCraftItem(CraftingConfigData craftConfig)
        {
            _onCraftItem.OnNext(craftConfig);
        }
    }
}