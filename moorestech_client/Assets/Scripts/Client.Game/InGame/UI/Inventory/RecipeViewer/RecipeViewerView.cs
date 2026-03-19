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

            // アンロック済みレシピを1回だけ取得して各ビューに渡す
            // Compute unlocked machine recipes once and pass to each view
            var unlockedMachineRecipes = recipeViewerItemRecipes.UnlockedMachineRecipes();

            craftInventoryView.SetRecipes(recipeViewerItemRecipes);
            machineRecipeView.SetRecipes(recipeViewerItemRecipes, unlockedMachineRecipes);
            recipeTabView.SetRecipeTabView(recipeViewerItemRecipes, unlockedMachineRecipes);

            // クラフトレシピがある場合はそれを最初に表示する
            // Show craft recipes first if available
            var isFirstCraft = recipeViewerItemRecipes.UnlockedCraftRecipes().Count != 0;
            craftInventoryView.SetActive(isFirstCraft);
            machineRecipeView.SetActive(!isFirstCraft);

            // アンロック済み機械レシピがあれば表示
            // Show unlocked machine recipes if available
            if (!isFirstCraft && unlockedMachineRecipes.Count != 0)
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