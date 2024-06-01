using System;
using UniRx;

namespace Game.Crafting.Interface
{
    public class CraftEvent
    {
        private readonly Subject<CraftingConfigInfo> _onCraftItem = new();
        public IObservable<CraftingConfigInfo> OnCraftItem => _onCraftItem;
        
        public void InvokeCraftItem(CraftingConfigInfo craftConfig)
        {
            _onCraftItem.OnNext(craftConfig);
        }
    }
}