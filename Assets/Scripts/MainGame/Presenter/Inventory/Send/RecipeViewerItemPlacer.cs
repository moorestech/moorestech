using Game.PlayerInventory.Interface;
using MainGame.Presenter.Inventory.Receive;
using MainGame.UnityView.UI.CraftRecipe;
using Server.Protocol.PacketResponse.Util;
using VContainer.Unity;

namespace MainGame.Presenter.Inventory.Send
{
    public class RecipeViewerItemPlacer : IInitializable
    {
        private readonly CraftingInventoryViewPresenter _craftingInventoryViewPresenter;

        public RecipeViewerItemPlacer(RecipePlaceButton recipePlaceButton,CraftingInventoryViewPresenter craftingInventoryViewPresenter)
        {
            _craftingInventoryViewPresenter = craftingInventoryViewPresenter;
            recipePlaceButton.OnClick += ReplaceCraftRecipe;
        }


        private void ReplaceCraftRecipe(ViewerRecipeData viewerRecipeData)
        {
            if (viewerRecipeData.RecipeType != ViewerRecipeType.Craft) return;
            
            //クラフトインベントリのアイテムをすべてMainに移動する
            for (int i = 0; i < PlayerInventoryConst.CraftingInventoryColumns; i++)
            {
                // クラフトアイテムをすべてメインに移動する
                var itemCount = _craftingInventoryViewPresenter.CraftingInventory[i].Count;
                _inventoryMoveItemProtocol.Send(
                    itemCount,
                    ItemMoveType.InsertSlot,
                    new FromItemMoveInventoryInfo(ItemMoveInventoryType.CraftInventory,i),
                    new ToItemMoveInventoryInfo(ItemMoveInventoryType.MainInventory));
            }

        }

        public void Initialize() { }
    }
}