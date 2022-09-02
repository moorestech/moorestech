using Game.PlayerInventory.Interface;
using MainGame.Network.Send;
using MainGame.UnityView.UI.CraftRecipe;
using Server.Protocol.PacketResponse.Util;
using Server.Protocol.PacketResponse.Util.InventoryMoveUitl;
using Server.Protocol.PacketResponse.Util.InventoryMoveUtil;
using VContainer.Unity;

namespace MainGame.Presenter.Inventory.Send
{
    /// <summary>
    /// アイテム設置のボタンが押されたときにデータを送信するPresenter
    /// </summary>
    public class RecipeViewerItemPlacer : IInitializable
    {
        private readonly SendSetRecipeCraftingInventoryProtocol _setRecipeCraftingInventoryProtocol;

        public RecipeViewerItemPlacer(RecipePlaceButton recipePlaceButton,SendSetRecipeCraftingInventoryProtocol setRecipeCraftingInventoryProtocol)
        {
            _setRecipeCraftingInventoryProtocol = setRecipeCraftingInventoryProtocol;
            recipePlaceButton.OnClick += ReplaceCraftRecipe;
        }


        private void ReplaceCraftRecipe(ViewerRecipeData viewerRecipeData)
        {
            if (viewerRecipeData.RecipeType != ViewerRecipeType.Craft) return;
            
            _setRecipeCraftingInventoryProtocol.Send(viewerRecipeData.ItemStacks);
        }

        public void Initialize() { }
    }
}