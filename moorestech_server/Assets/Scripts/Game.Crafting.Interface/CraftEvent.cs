using System;
using UniRx;

namespace Game.Crafting.Interface
{
    public class CraftEvent
    {
        public IObservable<CraftingConfigInfo> OnCraftItem => _onCraftItem;
        private readonly Subject<CraftingConfigInfo> _onCraftItem = new();
        
        public void InvokeCraftItem(CraftingConfigInfo craftConfig)
        {
            _onCraftItem.OnNext(craftConfig);
        }
    }
}