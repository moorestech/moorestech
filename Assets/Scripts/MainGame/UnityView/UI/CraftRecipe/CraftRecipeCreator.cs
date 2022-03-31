using MoorestechSinglePlayInterface;

namespace MainGame.UnityView.UI.CraftRecipe
{
    public class CraftRecipeCreator
    {
        public CraftRecipeCreator(ItemListViewer itemListViewer,SinglePlayInterface singlePlayInterface)
        {
            itemListViewer.OnItemListClick += OnItemListClick;
        }

        private void OnItemListClick(int itemId)
        {
            
        }
    }
}