using System;
using UniRx;

namespace Game.Crafting.Interface
{
    public class CraftEvent
    {
        public IObserver<CraftingItemData> OnCraftItem => _onCraftItem;
        private readonly Subject<CraftingItemData> _onCraftItem = new();
        
        public void InvokeCraftItem(CraftingItemData craftingItemData)
        {
            _onCraftItem.OnNext(craftingItemData);
        }
    }
}