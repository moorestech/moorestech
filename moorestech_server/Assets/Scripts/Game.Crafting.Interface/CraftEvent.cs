using System;
using Mooresmaster.Model.CraftRecipesModule;
using UniRx;

namespace Game.Crafting.Interface
{
    public class CraftEvent
    {
        private readonly Subject<CraftRecipeMasterElement> _onCraftItem = new();
        public IObservable<CraftRecipeMasterElement> OnCraftItem => _onCraftItem;
        
        public void InvokeCraftItem(CraftRecipeMasterElement craftMasterElement)
        {
            _onCraftItem.OnNext(craftMasterElement);
        }
    }
}