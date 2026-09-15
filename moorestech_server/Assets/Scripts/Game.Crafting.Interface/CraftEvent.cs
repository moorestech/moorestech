using System;
using Mooresmaster.Model.CraftRecipesModule;
using UniRx;

namespace Game.Crafting.Interface
{
    public class CraftEvent
    {
        private readonly Subject<(int playerId, CraftRecipeMasterElement craftRecipe)> _onCraftItem = new();
        public IObservable<(int playerId, CraftRecipeMasterElement craftRecipe)> OnCraftItem => _onCraftItem;

        public void InvokeCraftItem(int playerId, CraftRecipeMasterElement craftMasterElement)
        {
            _onCraftItem.OnNext((playerId, craftMasterElement));
        }
    }
}
