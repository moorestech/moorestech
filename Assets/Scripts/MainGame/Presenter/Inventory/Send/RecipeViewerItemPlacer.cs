using MainGame.Network.Send;
using MainGame.UnityView.UI.CraftRecipe;
using MainGame.UnityView.UI.Inventory.Control;
using VContainer.Unity;

namespace MainGame.Presenter.Inventory.Send
{
    public class RecipeViewerItemPlacer : IInitializable
    {
        private InventoryMoveItemProtocol _inventoryMoveItemProtocol;
        private PlayerInventoryViewModel _playerInventoryViewModel;

        public RecipeViewerItemPlacer(InventoryMoveItemProtocol inventoryMoveItemProtocol, RecipePlaceButton recipePlaceButton, PlayerInventoryViewModel playerInventoryViewModel)
        {
            _inventoryMoveItemProtocol = inventoryMoveItemProtocol;
            _playerInventoryViewModel = playerInventoryViewModel;
            recipePlaceButton.OnClick += ReplaceCraftRecipe;
        }


        private void ReplaceCraftRecipe(ViewerRecipeData viewerRecipeData)
        {
            if (viewerRecipeData.RecipeType == ViewerRecipeType.Craft)
            {
                
                
            
                
            }
            
        }

        public void Initialize() { }
    }
}