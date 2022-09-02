using Game.PlayerInventory.Interface;
using MainGame.UnityView.UI.CraftRecipe;
using Server.Protocol.PacketResponse.Util;
using Server.Protocol.PacketResponse.Util.InventoryMoveUitl;
using Server.Protocol.PacketResponse.Util.InventoryMoveUtil;
using VContainer.Unity;

namespace MainGame.Presenter.Inventory.Send
{
    public class RecipeViewerItemPlacer : IInitializable
    {
        public RecipeViewerItemPlacer(RecipePlaceButton recipePlaceButton)
        {
            recipePlaceButton.OnClick += ReplaceCraftRecipe;
        }


        private void ReplaceCraftRecipe(ViewerRecipeData viewerRecipeData)
        {
            if (viewerRecipeData.RecipeType != ViewerRecipeType.Craft) return;
            

        }

        public void Initialize() { }
    }
}