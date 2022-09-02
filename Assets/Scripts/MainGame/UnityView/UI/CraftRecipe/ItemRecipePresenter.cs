using System.Collections.Generic;
using System.Linq;
using SinglePlay;
using UnityEngine;
using VContainer;

namespace MainGame.UnityView.UI.CraftRecipe
{
    public class ItemRecipePresenter : MonoBehaviour
    {
        private readonly Dictionary<int, List<ViewerRecipeData>> _itemIdToRecipe = new();

        public ViewerRecipeData CurrentViewerRecipeData { get; private set; }

        private  ItemRecipeView _itemRecipeView;
        
        public bool IsClicked => _isClickedCount == 0 || _isClickedCount == 1;
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
                    
                    list.Add(new ViewerRecipeData(recipe.ItemInputs,resultItem,ViewerRecipeType.Machine,recipe.BlockId));
                }
            }
            
            
            
            //アイテムリストからアイテムをクリックした時のイベントをサブスクライブ
            craftRecipeItemListViewer.OnItemListClick += OnItemListClick;
            itemRecipeView.OnCraftSlotClick += OnItemListClick;
            _itemRecipeView = itemRecipeView;
        }

        private void OnItemListClick(int itemId)
        {
            if (!_itemIdToRecipe.ContainsKey(itemId))
            {
                return;
            }

            _isClickedCount = 0;
            
            //TODO 複数レシピに対応させる
            var recipe = _itemIdToRecipe[itemId][0];
            CurrentViewerRecipeData = recipe;
            
            switch (recipe.RecipeType)
            {
                case ViewerRecipeType.Craft:
                    _itemRecipeView.SetCraftRecipe(recipe.ItemStacks,recipe.ResultItem[0]);
                    break;
                case ViewerRecipeType.Machine:
                    _itemRecipeView.SetMachineCraftRecipe(recipe.ItemStacks,recipe.ResultItem[0],recipe.BlockId);
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