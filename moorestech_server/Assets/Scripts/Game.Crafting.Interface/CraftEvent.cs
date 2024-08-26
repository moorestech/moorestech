using System;
using Mooresmaster.Model.CraftRecipesModule;
using UniRx;

namespace Game.Crafting.Interface
{
    public class CraftEvent
    {
        private readonly Subject<CraftRecipeElement> _onCraftItem = new();
        public IObservable<CraftRecipeElement> OnCraftItem => _onCraftItem;
        
        public void InvokeCraftItem(CraftRecipeElement craftConfig)
        {
            _onCraftItem.OnNext(craftConfig);
        }
    }
}