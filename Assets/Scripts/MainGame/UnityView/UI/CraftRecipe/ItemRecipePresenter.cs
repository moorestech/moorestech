using System.Collections.Generic;
using System.Linq;
using Core.Block.Config;
using SinglePlay;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace MainGame.UnityView.UI.CraftRecipe
{
    public class ItemRecipePresenter : MonoBehaviour
    {
        [SerializeField] private Button nextRecipeButton;
        [SerializeField] private Button prevRecipeButton;
        
        private readonly Dictionary<int, List<ViewerRecipeData>> _itemIdToRecipe = new();
        public ViewerRecipeData CurrentViewerRecipeData { get; private set; }
        private  ItemRecipeView _itemRecipeView;
        
        public bool IsClicked => _isClickedCount == 0 || _isClickedCount == 1;
        
        /// <summary>
        /// レシピビューワーがクリックされたかどうかを検知するためのカウント
        /// 
        /// TODO ここはイベント駆動とかにしてもいいかもしれん、何かいい方法を探す
        /// </summary>
        private int _isClickedCount = -1;
        

        [Inject]
        public void Construct(CraftRecipeItemListViewer craftRecipeItemListViewer,SinglePlayInterface singlePlayInterface,ItemRecipeView itemRecipeView)
        {
            //レシピ表示用のDictionaryを構築する
            var craftRecipe = singlePlayInterface.CraftingConfig.GetCraftingConfigList();
            var machineRecipe = singlePlayInterface.MachineRecipeConfig.GetAllRecipeData();

            //手元クラフトの構築
            foreach (var recipe in craftRecipe)
            {
                if (!_itemIdToRecipe.TryGetValue(recipe.Result.Id,out var list))
                {
                    list = new();
                    _itemIdToRecipe[recipe.Result.Id] = list;
                }
                
                list.Add(new ViewerRecipeData(recipe.Items,recipe.Result,ViewerRecipeType.Craft));
            }
            
            var blockConfig = singlePlayInterface.BlockConfig;
            
            //機械レシピの構築
            foreach (var recipe in machineRecipe)
            {
                var resultItem = recipe.ItemOutputs.Select(o => o.OutputItem).ToList();
                foreach (var output in recipe.ItemOutputs)
                {
                    if (!_itemIdToRecipe.TryGetValue(output.OutputItem.Id,out var list))
                    {
                        list = new();
                        _itemIdToRecipe[output.OutputItem.Id] = list;
                    }
                    
                    var blockItemId = blockConfig.GetBlockConfig(recipe.BlockId).ItemId;
                    list.Add(new ViewerRecipeData(recipe.ItemInputs,resultItem,ViewerRecipeType.Machine,blockItemId));
                }
            }
            
            
            _itemRecipeView = itemRecipeView;
            //アイテムリストからアイテムをクリックした時にレシピを表示する
            craftRecipeItemListViewer.OnItemListClick += item => {
                DisplayRecipe(item,0);
                _currentRecipeIndex = 0;
            };
            itemRecipeView.OnCraftSlotClick += item => {
                DisplayRecipe(item,0);
                _currentRecipeIndex = 0;
            };
            
            //レシピの切り替え
            nextRecipeButton.onClick.AddListener(() => {
                if (_displayedItemId == -1)
                {
                    return;
                }
                
                _currentRecipeIndex++;
                if (_currentRecipeIndex >= _itemIdToRecipe[_displayedItemId].Count)
                {
                    _currentRecipeIndex = 0;
                }
                
                DisplayRecipe(_displayedItemId,_currentRecipeIndex);
            });
            
            prevRecipeButton.onClick.AddListener(() => {
                if (_displayedItemId == -1)
                {
                    return;
                }
                
                _currentRecipeIndex--;
                if (_currentRecipeIndex < 0)
                {
                    _currentRecipeIndex = _itemIdToRecipe[_displayedItemId].Count - 1;
                }
                
                DisplayRecipe(_displayedItemId,_currentRecipeIndex);
            });
        }



        
        
        
        
        
        private int _currentRecipeIndex = 0;
        private int _displayedItemId = -1;
        
        /// <summary>
        /// 実際にレシピを表示する処理
        /// </summary>
        /// <param name="itemId"></param>
        /// <param name="recipeIndex"></param>
        private void DisplayRecipe(int itemId,int recipeIndex)
        {
            if (!_itemIdToRecipe.ContainsKey(itemId))
            {
                return;
            }

            _displayedItemId = itemId;
            _isClickedCount = 0;
            
            var recipe = _itemIdToRecipe[itemId][recipeIndex];
            CurrentViewerRecipeData = recipe;

            nextRecipeButton.interactable = _itemIdToRecipe[itemId].Count > 1;
            prevRecipeButton.interactable = _itemIdToRecipe[itemId].Count > 1;
            
            switch (recipe.RecipeType)
            {
                case ViewerRecipeType.Craft:
                    _itemRecipeView.SetCraftRecipe(recipe.ItemStacks,recipe.ResultItem[0]);
                    break;
                case ViewerRecipeType.Machine:
                    _itemRecipeView.SetMachineCraftRecipe(recipe.ItemStacks,recipe.ResultItem[0],recipe.MachineItemId);
                    break;
            }
        }

        private void Update()
        {
            // ButtonがクリックされたことをUpdate内で確認したいのでクリックされてから2フレームはtrueとする
            if (_isClickedCount == 0 || _isClickedCount == 1)
            {
                _isClickedCount++;
            }
            if (_isClickedCount == 2)
            {
                _isClickedCount = -1;
            }
        }
    }

}