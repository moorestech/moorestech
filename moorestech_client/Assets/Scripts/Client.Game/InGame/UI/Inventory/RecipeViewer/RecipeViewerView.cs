using Client.Game.InGame.UI.Inventory.Sub;
using Core.Master;
using Game.CraftChainer.Util;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.UI.Inventory.RecipeViewer
{
    public class RecipeViewerView : MonoBehaviour
    {
        [SerializeField] private CraftInventoryView craftInventoryView;
        [SerializeField] private MachineRecipeView machineRecipeView;
        [SerializeField] private RecipeTabView recipeTabView;
        
        [SerializeField] private ItemListView itemListView;
        
        private void Awake()
        {
            itemListView.OnClickItem.Subscribe(SetItemListView);
            craftInventoryView.OnClickItem.Subscribe(SetItemListView);
            machineRecipeView.OnClickItem.Subscribe(SetItemListView);
            recipeTabView.OnClickTab.Subscribe(OnClickTab);
        }
        
        private void SetItemListView(RecipeViewerItemRecipes recipeViewerItemRecipes)
        {
            if (recipeViewerItemRecipes == null)
            {
                return;
            }
            
            craftInventoryView.SetRecipes(recipeViewerItemRecipes);
            machineRecipeView.SetRecipes(recipeViewerItemRecipes);
            recipeTabView.SetRecipeTabView(recipeViewerItemRecipes);
            
            // クラフトレシピがある場合はそれを最初に表示する
            var isFirstCraft = recipeViewerItemRecipes.UnlockedCraftRecipes().Count != 0;
            craftInventoryView.SetActive(isFirstCraft);
            machineRecipeView.SetActive(!isFirstCraft);
            if (isFirstCraft)
            {
                craftInventoryView.DisplayRecipe(0);
            }
            else if (recipeViewerItemRecipes.MachineRecipes.Count != 0)
            {
                machineRecipeView.DisplayRecipe(0);
            }
        }
        
        private void OnClickTab(BlockId? blockId)
        {
            var isCraft = !blockId.HasValue;
            
            if (isCraft)
            {
                craftInventoryView.SetActive(true);
                machineRecipeView.SetActive(false);
            }
            else
            {
                craftInventoryView.SetActive(false);
                machineRecipeView.SetActive(true);
                
                machineRecipeView.SetBlockId(blockId.Value);
            }
        }
        
        public void SetActive(bool isActive)
        {
            gameObject.SetActive(isActive);
        }
    }
}