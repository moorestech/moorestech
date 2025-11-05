using System.Collections.Generic;
using Client.Game.InGame.CraftTree.TreeView;
using Client.Game.InGame.UI.Inventory.Craft;
using Core.Master;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace Client.Game.InGame.UI.Inventory.RecipeViewer
{
    public class RecipeViewerView : MonoBehaviour
    {
        [SerializeField] private CraftInventoryView craftInventoryView;
        [SerializeField] private MachineRecipeView machineRecipeView;
        [SerializeField] private RecipeTabView recipeTabView;
        
        [SerializeField] private ItemListView itemListView;
        
        [SerializeField] private CraftTreeViewManager craftTreeViewManager;
        [SerializeField] private List<Button> createCraftTreeView;
        
        private RecipeViewerItemRecipes _currentRecipe;
        
        private void Awake()
        {
            itemListView.OnClickItem.Subscribe(SetItemListView);
            craftInventoryView.OnClickItem.Subscribe(SetItemListView);
            machineRecipeView.OnClickItem.Subscribe(SetItemListView);
            recipeTabView.OnClickTab.Subscribe(OnClickTab);
            
            foreach (var craftTreeView in createCraftTreeView)
            {
                craftTreeView.onClick.AddListener(() =>
                {
                    if (_currentRecipe == null) return;
                    
                    craftTreeViewManager.CreateNewCraftTree(_currentRecipe.ResultItemId);
                });
            }
        }
        
        private void SetItemListView(RecipeViewerItemRecipes recipeViewerItemRecipes)
        {
            if (recipeViewerItemRecipes == null)
            {
                return;
            }
            
            _currentRecipe = recipeViewerItemRecipes;
            
            craftInventoryView.SetRecipes(recipeViewerItemRecipes);
            machineRecipeView.SetRecipes(recipeViewerItemRecipes);
            recipeTabView.SetRecipeTabView(recipeViewerItemRecipes);
            
            // クラフトレシピがある場合はそれを最初に表示する
            var isFirstCraft = recipeViewerItemRecipes.UnlockedCraftRecipes().Count != 0;
            craftInventoryView.SetActive(isFirstCraft);
            machineRecipeView.SetActive(!isFirstCraft);
            
            // SetRecipesの中で最初のレシピが自動選択されるようになったので
            // DisplayRecipe(0)の呼び出しは不要
            if (!isFirstCraft && recipeViewerItemRecipes.MachineRecipes.Count != 0)
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